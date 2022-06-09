// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using NUnit.Framework;

    public static class NUnitLogs
    {
        public static async Task CollectAsync(DateTime testStartTime, CancellationToken token)
        {
            string prefix = $"{DeviceId.Current.BaseId}-{TestContext.CurrentContext.Test.NormalizedName()}";
            IEnumerable<string> paths = await EdgeLogs.CollectAsync(testStartTime, prefix, token);
            foreach (string path in paths)
            {
                TestContext.AddTestAttachment(path);
            }
        }
    }
}
