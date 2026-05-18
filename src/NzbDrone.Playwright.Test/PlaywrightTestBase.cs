using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
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
    // Equivalent of the legacy Selenium AutomationTest base class, ported
    // to Playwright. Lifecycle:
    //   OneTimeSetUp  → start NzbDroneRunner, launch Playwright Chromium,
    //                   open http://localhost:8787 and wait past the spinner
    //   per test      → uses _page (PageBase wrapper) for nav locators
    //   OneTimeTearDown → close browser, kill the runner
    //
    // NzbDroneRunner picks up the most-recently-built backend in
    // _output/net6.0 — run `./build.sh --backend` before invoking the
    // suite, or the runner will throw at OneTimeSetUp.
    public abstract class PlaywrightTestBase
    {
        protected IPlaywright Playwright;
        protected IBrowser Browser;
        protected IBrowserContext Context;
        protected IPage Page;
        protected PageBase _page;

        private NzbDroneRunner _runner;

        protected PlaywrightTestBase()
        {
            new StartupContext();

            LogManager.Configuration = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget { Layout = "${level}: ${message} ${exception}" };
            LogManager.Configuration.AddTarget(consoleTarget.GetType().Name, consoleTarget);
            LogManager.Configuration.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Trace, consoleTarget));
        }

        [OneTimeSetUp]
        public async Task SmokeTestSetup()
        {
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

            _page = new PageBase(Page);
            await _page.WaitForNoSpinner();

            // Match the Selenium suite — the frontend sets this to widen the
            // viewport on certain pages (the NameViews flag is read by Redux
            // selectors). See window.Readarr in frontend/src/index.ts.
            await Page.EvaluateAsync("() => { window.Readarr.NameViews = true; }");

            (await GetPageErrors()).Should().BeEmpty();
        }

        protected async Task<string[]> GetPageErrors()
        {
            return (await Page.Locator("#errors div").AllInnerTextsAsync()).ToArray();
        }

        protected async Task TakeScreenshot(string name)
        {
            try
            {
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = $"./{name}_test_screenshot.png",
                    FullPage = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save screenshot {name}, {ex.Message}");
            }
        }

        [OneTimeTearDown]
        public async Task SmokeTestTearDown()
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

        [TearDown]
        public async Task AutomationTearDown()
        {
            (await GetPageErrors()).Should().BeEmpty();
        }
    }
}
