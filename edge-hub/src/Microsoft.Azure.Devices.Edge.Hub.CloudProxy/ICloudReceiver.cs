// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;

    public interface ICloudReceiver
    {
        /// <summary>
        /// Init should start the pump to receive messages from IoTHub
        /// and pass them on to the ICloudListener.
        /// </summary>
        void Init(ICloudListener listener);
    }
}
