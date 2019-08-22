// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;
    using Serilog;

    public class BaseFixture
    {
        Profiler profiler;
        DateTime testStartTime;

        protected CancellationTokenSource cts;
        protected IotHub iotHub;
        protected EdgeRuntime runtime;

        [SetUp]
        protected void BeforeEachTest()
        {
            this.cts = new CancellationTokenSource(Context.Current.TestTimeout);
            this.testStartTime = DateTime.Now;
            this.profiler = Profiler.Start();
            this.iotHub = new IotHub(
                Context.Current.ConnectionString,
                Context.Current.EventHubEndpoint,
                Context.Current.Proxy);
            this.runtime = new EdgeRuntime(
                Context.Current.DeviceId,
                Context.Current.EdgeAgentImage,
                Context.Current.EdgeHubImage,
                Context.Current.Proxy,
                Context.Current.Registries,
                Context.Current.OptimizeForPerformance,
                this.iotHub);
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

                    if (TestContext.CurrentContext.Result.Outcome != ResultState.Ignored)
                    {
                        using (var cts = new CancellationTokenSource(Context.Current.TeardownTimeout))
                        {
                            await NUnitLogs.CollectAsync(this.testStartTime, cts.Token);
                        }
                    }
                },
                "Completed test teardown");
        }
    }
}
