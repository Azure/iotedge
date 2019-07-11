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

    public class ModuleBase
    {
        DateTime testStartTime;

        protected CancellationTokenSource cts;

        [SetUp]
        public void BeforeEach()
        {
            this.cts = new CancellationTokenSource(Context.Current.TestTimeout);
            this.testStartTime = DateTime.Now;
        }

        [TearDown]
        public async Task AfterEachAsync()
        {
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
