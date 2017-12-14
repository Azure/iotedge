// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using Microsoft.Azure.Devices.Client;

    public class DeviceClientProvider : IDeviceClientProvider
    {
        public IDeviceClient Create(string hostName, IAuthenticationMethod authenticationMethod, ITransportSettings[] transportSettings)
        {
            DeviceClient deviceClient = DeviceClient.Create(hostName, authenticationMethod, transportSettings);
            return new DeviceClientWrapper(deviceClient);
        }

        public IDeviceClient Create(string connectionString, ITransportSettings[] transportSettings)
        {
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, transportSettings);
            return new DeviceClientWrapper(deviceClient);
        }
    }
}
