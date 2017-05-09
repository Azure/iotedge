// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class ConnectionStringUtil
    {
        public static string GetDeviceIdFromConnectionString(string connectionString)
        {
            IotHubConnectionStringBuilder connectionStringBuilder = 
                IotHubConnectionStringBuilder.Create(Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString)));
            return connectionStringBuilder.DeviceId;
        }
    }
}
