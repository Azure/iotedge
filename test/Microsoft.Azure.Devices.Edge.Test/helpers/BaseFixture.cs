// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;
    using Serilog;

    public class BaseFixture
    {
        CancellationTokenSource cts;
        Profiler profiler;
        DateTime testStartTime;

        protected CancellationToken TestToken => this.cts.Token;
        protected IotedgeCli cli;

        protected virtual Task BeforeTestTimerStarts() => Task.CompletedTask;
        protected virtual Task AfterTestTimerEnds() => Task.CompletedTask;

        [SetUp]
        protected async Task BeforeEachTestAsync()
        {
            await this.BeforeTestTimerStarts();
            this.cts = new CancellationTokenSource(Context.Current.TestTimeout);
            this.testStartTime = DateTime.Now;
            this.profiler = Profiler.Start();
            Log.Information("Running test '{Name}'", TestContext.CurrentContext.Test.Name);
        }

        [TearDown]
        protected async Task AfterEachTestAsync()
        {
            this.profiler.Stop("Completed test '{Name}'", TestContext.CurrentContext.Test.Name);
            await Profiler.Run(
                async () =>
                {
                    this.cts.Dispose();
                    if ((!Context.Current.ISA95Tag) && (TestContext.CurrentContext.Result.Outcome != ResultState.Ignored))
                    {
                        using var cts = new CancellationTokenSource(Context.Current.TeardownTimeout);
                        await NUnitLogs.CollectAsync(this.testStartTime, this.cli, cts.Token);
                        if (Context.Current.GetSupportBundle)
                        {
                            try
                            {
                                var supportBundlePath = Context.Current.LogFile.Match((file) => Path.GetDirectoryName(file), () => AppDomain.CurrentDomain.BaseDirectory);
                                await this.cli.RunAsync(
                                    $"support-bundle -o {supportBundlePath}/supportbundle-{TestContext.CurrentContext.Test.Name} --since \"{this.testStartTime:yyyy-MM-ddTHH:mm:ssZ}\"",
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
