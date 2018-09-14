// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IDeviceScopeIdentitiesCache
    {
        Task<Option<ServiceIdentity>> GetServiceIdentity(string id);

        Task<Option<ServiceIdentity>> GetServiceIdentity(string deviceId, string moduleId);

        void InitiateCacheRefresh();

        Task RefreshServiceIdentities(IEnumerable<string> deviceIds);

        Task RefreshServiceIdentity(string deviceId);

        Task RefreshServiceIdentity(string deviceId, string moduleId);

        event EventHandler<ServiceIdentity> ServiceIdentityUpdated;

        event EventHandler<string> ServiceIdentityRemoved;
    }
}
