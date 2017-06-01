// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    class CloudListener : ICloudListener
    {
        readonly IDeviceProxy deviceProxy;

        public CloudListener(IDeviceProxy deviceProxy)
        {
            this.deviceProxy = deviceProxy;
        }

        public Task CallMethodAsync(DirectMethodRequest request) => this.deviceProxy.CallMethodAsync(request);

        public Task OnDesiredPropertyUpdates(IMessage desiredProperties) => this.deviceProxy.OnDesiredPropertyUpdates(desiredProperties);

        public Task ProcessMessageAsync(IMessage message) => this.deviceProxy.SendMessageAsync(message);
    }
}
