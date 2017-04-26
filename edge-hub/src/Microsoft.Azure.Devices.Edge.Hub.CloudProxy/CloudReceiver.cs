// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;

    // TODO: Make sure the instance does not get garbage collected
    class CloudReceiver : ICloudReceiver
    {
        readonly DeviceClient deviceClient;

        public CloudReceiver(DeviceClient deviceClient)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
        }

        public void Init(ICloudListener listener)
        {
            Preconditions.CheckNotNull(listener, nameof(listener));
            this.SetupCallMethod(listener.CallMethod);
        }

        void SetupCallMethod(Func<string, object, string, Task<object>> callMethod)
        {
            // TODO - to be implemented
        }
    }
}
