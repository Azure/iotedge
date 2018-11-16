// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ICloudConnectionProvider
    {
        /// <summary>
        /// Creates a connection to the cloud using the provided client credentials
        /// </summary>
        Task<Try<ICloudConnection>> Connect(IClientCredentials clientCredentials, Action<string, CloudConnectionStatus> connectionStatusChangedHandler);

        /// <summary>
        /// Creates a connection to the cloud for a client in device scope
        /// </summary>
        Task<Try<ICloudConnection>> Connect(IIdentity identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler);

        /// <summary>
        /// Binds the IEdgeHub instance to the object
        /// </summary>
        /// <param name="edgeHub"></param>
        void BindEdgeHub(IEdgeHub edgeHub);
    }
}
