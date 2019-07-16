// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;
    using Serilog;

    public class TestBase
    {
        Profiler profiler;
        DateTime testStartTime;

        protected CancellationTokenSource cts;

        [SetUp]
        protected void BeforeEachTest()
        {
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

                    if (TestContext.CurrentContext.Result.Outcome != ResultState.Ignored)
                    {
                        using (var cts = new CancellationTokenSource(Context.Current.TeardownTimeout))
                        {
                            string prefix = $"{Context.Current.DeviceId}-{TestContext.CurrentContext.Test.NormalizedName()}";
                            IEnumerable<string> paths = await EdgeLogs.CollectAsync(this.testStartTime, prefix, cts.Token);
                            foreach (string path in paths)
                            {
                                TestContext.AddTestAttachment(path);
                            }
                        }
                    }
                },
                "Completed test teardown");
        }
    }
}
