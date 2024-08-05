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

        protected virtual Task BeforeTestTimerStarts() => Task.CompletedTask;
        protected virtual Task AfterTestTimerEnds() => Task.CompletedTask;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public async Task BeforeEachTestAsync()
        {
            await this.BeforeTestTimerStarts();
            cts = new CancellationTokenSource(Context.Current.TestTimeout);
            testStartTime = DateTime.Now;
            string name = this.TestContext.TestName;
            if (this.TestContext.ManagedMethod.EndsWith(")"))
            {
                // Can't find a way to see the arguments to a DataRow test inside [TestInitialize],
                // so we'll just add elipses to indicate this is a DataRow test. The [TestCleanup]
                // method below will list the arguments, so at least there's that.
                name += "(...)";
            }

            profiler = Profiler.Start();
            Log.Information("Running test '{Name}'", name);
        }

        [TestCleanup]
        public async Task AfterEachTestAsync()
        {
            string name = this.TestContext.TestName;
            if (this.TestContext.Properties.Contains("Row"))
            {
                name += $"({this.TestContext.Properties["Row"]})";
            }

            profiler.Stop("Completed test '{Name}'", name);
            await Profiler.Run(
                async () =>
                {
                    cts.Dispose();
                    if ((!Context.Current.ISA95Tag) && (this.TestContext.CurrentTestOutcome != UnitTestOutcome.Inconclusive))
                    {
                        using var cts = new CancellationTokenSource(Context.Current.TeardownTimeout);
                        await NUnitLogs.CollectAsync(testStartTime, this.TestContext, cts.Token);
                        if (Context.Current.GetSupportBundle)
                        {
                            try
                            {
                                var supportBundlePath = Context.Current.LogFile.Match((file) => Path.GetDirectoryName(file), () => AppDomain.CurrentDomain.BaseDirectory);
                                await Process.RunAsync(
                                    "iotedge",
                                    $"support-bundle -o {supportBundlePath}/supportbundle-{this.TestContext.TestName} --since \"{testStartTime:yyyy-MM-ddTHH:mm:ssZ}\"",
                                    cts.Token);
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Failed to get support bundle log, error: {ex}");
                            }
                        }
                    }
                },
                "Completed test teardown");

            await this.AfterTestTimerEnds();
        }
    }
}
