// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;

    class CloudListener : ICloudListener
    {
        readonly string deviceId;
        readonly IDeviceProxy deviceProxy;

        public CloudListener(string deviceId, IDeviceProxy deviceProxy)
        {
            this.deviceId = deviceId;
            this.deviceProxy = deviceProxy;
        }

        public async Task<object> CallMethod(string methodName, object parameters, string deviceId)
        {
            return await this.deviceProxy.CallMethod(methodName, parameters);
        }

        public async Task ReceiveMessage(IMessage message)
        {
            await this.deviceProxy.SendMessage(message);
        }
    }
}
