// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using Azure.Devices.Edge.Util;

    public interface IDeviceScopeApiClientProvider
    {
        IDeviceScopeApiClient CreateDeviceScopeClient();

        IDeviceScopeApiClient CreateNestedDeviceScopeClient();

        IDeviceScopeApiClient CreateOnBehalfOf(string childDeviceId, Option<string> continuationLink);
    }
}
