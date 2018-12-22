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
        Task<Option<ServiceIdentity>> GetServiceIdentity(string id, bool refreshIfNotExists = false);

        void InitiateCacheRefresh();

        Task RefreshServiceIdentities(IEnumerable<string> ids);

        Task RefreshServiceIdentity(string id);

        event EventHandler<ServiceIdentity> ServiceIdentityUpdated;

        event EventHandler<string> ServiceIdentityRemoved;
    }
}
