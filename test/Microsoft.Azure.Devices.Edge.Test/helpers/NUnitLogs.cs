// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;

    public static class NUnitLogs
    {
        public static async Task CollectAsync(DateTime testStartTime, TestContext msTestContext, CancellationToken token)
        {
            string prefix = $"{DeviceId.Current.BaseId}-{msTestContext.NormalizedName()}";
            IEnumerable<string> paths = await EdgeLogs.CollectAsync(testStartTime, prefix, token);
            foreach (string path in paths)
            {
                msTestContext.AddResultFile(path);
            }
        }
    }
}
