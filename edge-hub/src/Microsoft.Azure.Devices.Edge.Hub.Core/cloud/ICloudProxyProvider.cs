// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ICloudProxyProvider
    {
        /// <summary>
        /// Connect sets up the CloudProxy
        /// </summary>
        Task<Try<ICloudProxy>> Connect(IIdentity identity, Action<ConnectionStatus, ConnectionStatusChangeReason> connectionStatusChangedHandler);
    }
}
