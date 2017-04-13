// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;

    public class CloudProxyProvider : ICloudProxyProvider
    {
        public ICloudProxy Connect(string connectionString, ICloudListener cloudListener)
        {
            Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));

            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString);
            ICloudProxy cloudProxy = new CloudProxy(deviceClient);
            ICloudReceiver cloudReceiver = new CloudReceiver(deviceClient);
            cloudReceiver.Init(cloudListener);
            return cloudProxy;
        }
    }
}
