// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeAgent : EdgeModule
    {
        public EdgeAgent(string deviceId, IotHub iotHub)
            : base("edgeAgent", deviceId, iotHub)
        {
        }

        public Task PingAsync(CancellationToken token)
        {
            return Profiler.Run(
                () => this.iotHub.InvokeMethodAsync(
                    this.deviceId,
                    $"${this.Id}",
                    new CloudToDeviceMethod("ping"),
                    token),
                "Pinged module '{Module}' from the cloud",
                this.Id);
        }
    }
}
