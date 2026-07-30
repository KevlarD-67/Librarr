using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Playwright.Test.PageModel;
using NzbDrone.Test.Common;

namespace NzbDrone.Playwright.Test
{
    // Assembly-wide gate and lifecycle for the Playwright smoke suite. Every
    // fixture here drives a headless Chromium against a real Librarr. The
    // default `dotnet test` invocation in a dev sandbox doesn't have a built
    // binary on hand and may not have Playwright's browser bundle installed,
    // so we don't fail it by default — opt in with READARR_RUN_PLAYWRIGHT=1.
    //
    // First-run setup: see README.md for the `playwright install` step.
    //
    // The browser and the Librarr instance live HERE, not on the test base,
    // and this is load-bearing. NzbDroneRunner.KillAll() kills every Readarr
    // process by name rather than only its own, and every fixture wants port
    // 8787 — so with per-fixture lifecycles, one fixture's teardown would
    // shoot down another fixture's instance and the suite failed
    // intermittently with TargetClosedException from OneTimeSetUp. One
    // instance per assembly removes the race by construction, and boots once
    // instead of once per fixture.
    [SetUpFixture]
    public class AssemblyGate
    {
        internal static IPlaywright Playwright;
        internal static IBrowser Browser;
        internal static IBrowserContext Context;
        internal static IPage Page;
        internal static PageBase PageModel;

        private static NzbDroneRunner _runner;

        [OneTimeSetUp]
        public async Task GateAssembly()
        {
            if (Environment.GetEnvironmentVariable("READARR_RUN_PLAYWRIGHT") != "1")
            {
                Assert.Ignore(
                    "Playwright smoke suite is opt-in; set READARR_RUN_PLAYWRIGHT=1 to run. " +
                    "Requires a built backend on disk and `playwright install` to have run once — see README.md.");
            }

            new StartupContext();

            LogManager.Configuration = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget { Layout = "${level}: ${message} ${exception}" };
            LogManager.Configuration.AddTarget(consoleTarget.GetType().Name, consoleTarget);
            LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Trace, consoleTarget));

            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();

            Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Timeout = (float)TimeSpan.FromMinutes(3).TotalMilliseconds
            });

            Context = await Browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });

            Page = await Context.NewPageAsync();

            _runner = new NzbDroneRunner(LogManager.GetCurrentClassLogger(), null);
            _runner.KillAll();
            _runner.Start(true);

            await Page.GotoAsync("http://localhost:8787");

            PageModel = new PageBase(Page);
            await PageModel.WaitForNoSpinner();

            // Match the Selenium suite — the frontend reads this to widen the
            // viewport on certain pages. See window.Readarr in frontend/src/index.ts.
            await Page.EvaluateAsync("() => { window.Readarr.NameViews = true; }");
        }

        [OneTimeTearDown]
        public async Task ReleaseAssembly()
        {
            _runner?.KillAll();

            if (Context != null)
            {
                await Context.CloseAsync();
            }

            if (Browser != null)
            {
                await Browser.CloseAsync();
            }

            Playwright?.Dispose();
        }
    }
}
