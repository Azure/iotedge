// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System.Threading;
    using System.Threading.Tasks;

    public class EdgeAgent : EdgeModule
    {
        public EdgeAgent(string deviceId, IotHub iotHub)
            : base("edgeAgent", deviceId, iotHub)
        {
        }

        public Task PingAsync(CancellationToken token) =>
            Profiler.Run(
                () => this.iotHub.InvokeMethodAsync(
                    this.deviceId,
                    $"${this.Id}",
                    new CloudToDeviceMethod("ping"),
                    token),
                "Pinged module '{Module}' from the cloud",
                this.Id);
    }
}
