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
        protected static TestContext msTestContext;

        protected virtual Task BeforeTestTimerStarts() => Task.CompletedTask;
        protected virtual Task AfterTestTimerEnds() => Task.CompletedTask;

        [TestInitialize]
        public async Task BeforeEachTestAsync()
        {
            await BeforeTestTimerStarts();
            cts = new CancellationTokenSource(Context.Current.TestTimeout);
            testStartTime = DateTime.Now;
            profiler = Profiler.Start();
            Log.Information("Running test '{Name}'", msTestContext.TestName);
        }

        [TestCleanup]
        public async Task AfterEachTestAsync()
        {
            profiler.Stop("Completed test '{Name}'", msTestContext.TestName);
            await Profiler.Run(
                async () =>
                {
                    cts.Dispose();
                    if ((!Context.Current.ISA95Tag) && (msTestContext.CurrentTestOutcome != UnitTestOutcome.Inconclusive))
                    {
                        using var cts = new CancellationTokenSource(Context.Current.TeardownTimeout);
                        await NUnitLogs.CollectAsync(testStartTime, msTestContext, cli, cts.Token);
                        if (Context.Current.GetSupportBundle)
                        {
                            try
                            {
                                var supportBundlePath = Context.Current.LogFile.Match((file) => Path.GetDirectoryName(file), () => AppDomain.CurrentDomain.BaseDirectory);
                                await cli.RunAsync(
                                    $"support-bundle -o {supportBundlePath}/supportbundle-{msTestContext.TestName} --since \"{testStartTime:yyyy-MM-ddTHH:mm:ssZ}\"",
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
