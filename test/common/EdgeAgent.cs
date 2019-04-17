// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Edge.Util;

namespace common
{
    public class EdgeAgent : EdgeModule
    {
        public EdgeAgent(string deviceId, CloudContext context)
            : base("edgeAgent", deviceId, context) { }

        public Task PingAsync(CancellationToken token)
        {
            return Profiler.Run(
                "Pinging module 'edgeAgent' from the cloud",
                () =>
                {
                    return Retry.Do(
                        () =>
                        {
                            return this.CloudContext.InvokeMethodAsync(
                                this.DeviceId,
                                "$edgeAgent",
                                new CloudToDeviceMethod("ping"),
                                token
                            );
                        },
                        result => result.Status == 200,
                        e => true,
                        TimeSpan.FromSeconds(5),
                        token
                    );
                }
            );
        }
    }
}