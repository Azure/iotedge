// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;
    using Serilog;

    public static class NUnitLogs
    {
        // Make an effort to collect logs, but swallow any exceptions to prevent tests/fixtures
        // from failing if this function fails.
        public static async Task CollectAsync(DateTime testStartTime, CancellationToken token)
        {
            try
            {
                string prefix = $"{Context.Current.DeviceId}-{TestContext.CurrentContext.Test.NormalizedName()}";
                IEnumerable<string> paths = await EdgeLogs.CollectAsync(testStartTime, prefix, token);
                foreach (string path in paths)
                {
                    TestContext.AddTestAttachment(path);
                }
            }
            catch (Exception e)
            {
                Log.Warning("Log collection failed for context " +
                    $"'{TestContext.CurrentContext.Test.Name}' with exception:\n{e.ToString()}");
            }
        }
    }
}