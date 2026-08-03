using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Http;
using NzbDrone.Common.Http.Dispatchers;
using NzbDrone.Common.Http.Proxy;
using NzbDrone.Test.Common;

namespace NzbDrone.Common.Test.Http
{
    [TestFixture]
    public class ManagedHttpDispatcherFixture : TestBase<ManagedHttpDispatcher>
    {
        private ICacheManager _cacheManager;

        [SetUp]
        public void SetUp()
        {
            // Held so a test can reach the very CredentialCache the dispatcher
            // will use, and read it the way SocketsHttpHandler does.
            _cacheManager = new CacheManager();
            Mocker.SetConstant(_cacheManager);

            Mocker.GetMock<IUserAgentBuilder>()
                .Setup(c => c.GetUserAgent(It.IsAny<bool>()))
                .Returns("Librarr-Test/1.0");

            Mocker.GetMock<IHttpProxySettingsProvider>()
                .Setup(c => c.GetProxySettings(It.IsAny<HttpUri>()))
                .Returns((HttpProxySettings)null);
        }

        // Regression test for the Calibre scan crash: two concurrent requests
        // to the same URL with NetworkCredential both mutated the dispatcher's
        // shared CredentialCache, which System.Net documents as not
        // thread-safe. Both threads could pass Remove() (nothing there yet)
        // and then race on Add(), and the loser threw
        // "An item with the same key has already been added."
        //
        // In the field the two callers were CalibreRootFolderCheck (scheduled
        // health check) and DiskScanService (library rescan) -- the only two
        // callers of CalibreProxy.GetAllBookFilePaths -- hitting one Calibre
        // server at once. The visible symptom was not an error page but an
        // empty library: the scan aborted and logged "Scan folder is empty".
        //
        // No server is needed to exercise this. The credential mutation
        // happens up front in GetResponseAsync, before GetClient() is even
        // called, so pointing at a closed loopback port reaches the racy code
        // and then fails the send -- which this test ignores. That keeps it
        // network-free and fast enough for the blocking unit job.
        //
        // Confirmed to fail against the unfixed dispatcher, which is the only
        // thing that makes it a regression test: 26-37 collisions per 400
        // iterations, plus InvalidOperationException from the backing
        // Dictionary noticing its own corruption. With the lock, zero.
        [Test]
        public async Task should_not_race_on_the_shared_credential_cache_when_one_url_is_requested_concurrently()
        {
            var url = $"http://127.0.0.1:{GetClosedLoopbackPort()}/ajax/books/lib?ids=1";

            // Resolved once: letting eight tasks race TestBase's lazy Subject
            // would be testing the harness, not the dispatcher.
            var subject = Subject;
            var races = new ConcurrentBag<Exception>();

            var workers = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < 50; i++)
                {
                    var request = new HttpRequest(url)
                    {
                        Credentials = new NetworkCredential("calibre", "hunter2"),
                        RequestTimeout = TimeSpan.FromSeconds(5)
                    };

                    try
                    {
                        await subject.GetResponseAsync(request, new CookieContainer());
                    }
                    catch (HttpRequestException)
                    {
                        // Connection refused. Expected, and the whole point of
                        // aiming at a closed port.
                    }
                    catch (OperationCanceledException)
                    {
                        // Request timeout. Also uninteresting here.
                    }
                    catch (Exception ex)
                    {
                        // Anything else got here from the credential mutation,
                        // which is what is under test.
                        //
                        // Deliberately NOT filtered on exception type or stack
                        // contents. The first version of this test matched
                        // `StackTrace.Contains("CredentialCache")` and passed
                        // against the UNFIXED dispatcher, because in Release
                        // CredentialCache.Add is inlined: the trace runs
                        // Dictionary.TryInsert -> Dictionary.Add ->
                        // GetResponseAsync, with no CredentialCache frame to
                        // match on. Name the expected exceptions and treat
                        // everything else as failure, rather than trying to
                        // predict what corruption will look like.
                        races.Add(ex);
                    }
                }
            })).ToArray();

            await Task.WhenAll(workers);

            var summary = string.Join(", ", races
                .GroupBy(e => e.GetType().Name)
                .Select(g => $"{g.Key} x{g.Count()}"));

            races.Should().BeEmpty(
                "concurrent requests to one URL must not corrupt the shared CredentialCache, but got [{0}]; first: {1}",
                summary,
                races.FirstOrDefault()?.Message);
        }

        // Serializing the writers is not the whole story. The reader is
        // SocketsHttpHandler resolving credentials off the same shared cache
        // (ManagedHttpDispatcher.CreateHttpClient assigns it to
        // handler.Credentials), and it does not take the dispatcher's lock.
        //
        // Remove-then-Add leaves a window with no entry at all, so a read
        // landing inside it returns null and the request is sent
        // unauthenticated -- an intermittent 401, not a crash, which is why it
        // would never be reported as the crash this fixture's other test
        // covers. The dispatcher avoids the window by not writing when the
        // credentials are already correct.
        //
        // This reads the cache directly rather than through a real server,
        // because provoking the handler's own lookup needs a 401 challenge and
        // that would drag a listener and real auth into a unit test. Reading
        // the same instance without the lock models the same access.
        [Test]
        public async Task should_not_leave_the_credential_cache_empty_for_an_unsynchronized_reader()
        {
            var url = $"http://127.0.0.1:{GetClosedLoopbackPort()}/ajax/books/lib?ids=1";
            var uri = new Uri(url);
            var subject = Subject;

            // Prime it, so the reader is not just observing the empty cache it
            // starts life as.
            await Fire(subject, url);

            var shared = _cacheManager
                .GetCache<CredentialCache>(typeof(ManagedHttpDispatcher), "credentialcache")
                .Get("credentialCache", () => new CredentialCache());

            shared.GetCredential(uri, "Basic").Should().NotBeNull("the priming request should have registered credentials");

            var stop = false;
            var nulls = 0;
            var readerErrors = new ConcurrentBag<Exception>();

            var reader = new Thread(() =>
            {
                while (!Volatile.Read(ref stop))
                {
                    try
                    {
                        if (shared.GetCredential(uri, "Basic") == null)
                        {
                            Interlocked.Increment(ref nulls);
                        }
                    }
                    catch (Exception ex)
                    {
                        readerErrors.Add(ex);
                    }
                }
            });

            reader.Start();

            var workers = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(async () =>
                {
                    for (var i = 0; i < 50; i++)
                    {
                        await Fire(subject, url);
                    }
                }))
                .ToArray();

            await Task.WhenAll(workers);

            Volatile.Write(ref stop, true);
            reader.Join();

            nulls.Should().Be(0, "a reader must never observe the credential missing while requests are in flight");
            readerErrors.Should().BeEmpty("reading the cache must not throw while requests are in flight");
        }

        private static async Task Fire(ManagedHttpDispatcher subject, string url)
        {
            var request = new HttpRequest(url)
            {
                Credentials = new NetworkCredential("calibre", "hunter2"),
                RequestTimeout = TimeSpan.FromSeconds(5)
            };

            try
            {
                await subject.GetResponseAsync(request, new CookieContainer());
            }
            catch (HttpRequestException)
            {
                // Connection refused, as intended.
            }
            catch (OperationCanceledException)
            {
                // Timeout, equally uninteresting.
            }
        }

        // Bind on port 0 to have the OS hand out a free port, then release it.
        // Beats hard-coding a port number that CI might already be using.
        private static int GetClosedLoopbackPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }
    }
}
