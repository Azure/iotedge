// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;

    public class EdgeAgent : EdgeModule
    {
        public EdgeAgent(string deviceId, IotHub iotHub)
            : base(ModuleName.EdgeAgent, deviceId, iotHub)
        {
        }

        public Task PingAsync(CancellationToken token) =>
            Profiler.Run(
                () => this.iotHub.InvokeMethodAsync(
                    this.deviceId,
                    this.Id,
                    new CloudToDeviceMethod("ping"),
                    token),
                "Pinged module '{Module}' from the cloud",
                this.Id);

        public Task WaitForReportedConfigurationAsync(object expected, CancellationToken token) => Profiler.Run(
            () => this.WaitForReportedPropertyUpdatesInternalAsync(expected, token),
            "Edge Agent confirmed deployment");
    }
}
