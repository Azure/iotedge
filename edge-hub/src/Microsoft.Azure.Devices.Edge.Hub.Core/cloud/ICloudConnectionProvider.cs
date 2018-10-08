// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ICloudConnectionProvider
    {
        /// <summary>
        /// Binds the IEdgeHub instance to the object
        /// </summary>
        /// <param name="edgeHub">Edge Hub instance</param>
        void BindEdgeHub(IEdgeHub edgeHub);

        /// <summary>
        /// Connect sets up the connection to the cloud
        /// </summary>
        /// <param name="identity">Client credentials</param>
        /// <param name="connectionStatusChangedHandler">Connection status change handler</param>
        /// <returns>Try wrapped cloud connection task</returns>
        Task<Try<ICloudConnection>> Connect(IClientCredentials identity, Action<string, CloudConnectionStatus> connectionStatusChangedHandler);
    }
}
