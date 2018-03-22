// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ICloudConnection
    {
        Option<ICloudProxy> CloudProxy { get; }

        bool IsActive { get; }

        Task<bool> CloseAsync();

        Task<ICloudProxy> CreateOrUpdateAsync(IClientCredentials newCredentials);
    }
}
