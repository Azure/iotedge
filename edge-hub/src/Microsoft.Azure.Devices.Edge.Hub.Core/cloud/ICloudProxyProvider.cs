// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    public interface ICloudProxyProvider
    {
        /// <summary>
        /// Connect sets up the CloudProxy and CloudReceiver
        /// </summary>
        ICloudProxy Connect(string connectionString, ICloudListener listener);
    }
}
