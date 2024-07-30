// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Serilog;

    public class BaseFixture
    {
        static CancellationTokenSource cts;
        static Profiler profiler;
        static DateTime testStartTime;

        protected static CancellationToken TestToken => cts.Token;
        protected static IotedgeCli cli;

        protected virtual Task BeforeTestTimerStarts() => Task.CompletedTask;
        protected virtual Task AfterTestTimerEnds() => Task.CompletedTask;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public async Task BeforeEachTestAsync()
        {
            await this.BeforeTestTimerStarts();
            cts = new CancellationTokenSource(Context.Current.TestTimeout);
            testStartTime = DateTime.Now;
            profiler = Profiler.Start();
            Log.Information("Running test '{Name}'", this.TestContext.TestName);
        }

        [TestCleanup]
        public async Task AfterEachTestAsync()
        {
            profiler.Stop("Completed test '{Name}'", this.TestContext.TestName);
            await Profiler.Run(
                async () =>
                {
                    cts.Dispose();
                    if ((!Context.Current.ISA95Tag) && (this.TestContext.CurrentTestOutcome != UnitTestOutcome.Inconclusive))
                    {
                        using var cts = new CancellationTokenSource(Context.Current.TeardownTimeout);
                        await NUnitLogs.CollectAsync(testStartTime, this.TestContext, cli, cts.Token);
                        if (Context.Current.GetSupportBundle)
                        {
                            try
                            {
                                var supportBundlePath = Context.Current.LogFile.Match((file) => Path.GetDirectoryName(file), () => AppDomain.CurrentDomain.BaseDirectory);
                                await cli.RunAsync(
                                    $"support-bundle -o {supportBundlePath}/supportbundle-{this.TestContext.TestName} --since \"{testStartTime:yyyy-MM-ddTHH:mm:ssZ}\"",
                                    cts.Token);
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Failed to Get Support Bundle  Log with Error:{ex}");
                            }
                        }
                    }
                },
                "Completed test teardown");

            await this.AfterTestTimerEnds();
        }
    }
}
