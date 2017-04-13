// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;

    // TODO: Make sure the instance does not get garbage collected
    class CloudReceiver : ICloudReceiver
    {
        readonly DeviceClient deviceClient;

        public CloudReceiver(DeviceClient deviceClient)
        {
            Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.deviceClient = deviceClient;
        }

        public void Init(ICloudListener listener)
        {
            Preconditions.CheckNotNull(listener, nameof(listener));
            this.SetupReceiveMessage(listener.ReceiveMessage);
            this.SetupCallMethod(listener.CallMethod);
        }

        private void SetupCallMethod(Func<string, object, string, Task<object>> callMethod)
        {
            throw new NotImplementedException();
        }

        private void SetupReceiveMessage(Func<IMessage, Task> receiveMessage)
        {
            throw new NotImplementedException();
        }
    }
}
