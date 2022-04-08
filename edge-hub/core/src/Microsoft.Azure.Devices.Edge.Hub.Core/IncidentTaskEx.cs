// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public static class IncidentTaskEx
    {
        public static async Task TimeoutAfterSDKHang(this Task task, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource())
            {
                Task timerTask = Task.Delay(timeout, cts.Token);
                Task completedTask = await Task.WhenAny(task, timerTask);
                if (completedTask == timerTask)
                {
                    throw new EdgeHubCloudSDKException("SDK hanging");
                }

                cts.Cancel();
                await task;
            }
        }
    }
}
