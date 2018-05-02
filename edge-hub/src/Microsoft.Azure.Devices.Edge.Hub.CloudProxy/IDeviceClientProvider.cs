// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using Microsoft.Azure.Devices.Client;

    public interface IDeviceClientProvider
    {
        IDeviceClient Create(string hostName, IAuthenticationMethod authenticationMethod, ITransportSettings[] transportSettings);

        IDeviceClient Create(string connectionString, ITransportSettings[] transportSettings);

        IDeviceClient Create(ITransportSettings[] transportSettings);
    }
}
