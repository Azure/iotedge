// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    // TODO: Stub for using in interfaces. Should be deleted once we can use the DeviceClient type from IoTHub device SDK.
    class DeviceClient
    {
        internal static DeviceClient CreateFromConnectionString(string connectionString)
        {
            return new DeviceClient();
        }
    }
}
