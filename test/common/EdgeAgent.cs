// Copyright (c) Microsoft. All rights reserved.

namespace common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeAgent : EdgeModule
    {
        public EdgeAgent(string deviceId, IotHub iotHub)
            : base("edgeAgent", deviceId, iotHub) { }

        public Task PingAsync(CancellationToken token)
        {
            return Profiler.Run(
                "Pinging module 'edgeAgent' from the cloud",
                () =>
                {
                    return Retry.Do(
                        () =>
                        {
                            return this.IotHub.InvokeMethodAsync(
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
