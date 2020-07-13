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
        event EventHandler<string> ServiceIdentityRemoved;

        event EventHandler<ServiceIdentity> ServiceIdentityUpdated;

        Task<Option<ServiceIdentity>> GetServiceIdentity(string id);

        Task<IList<ServiceIdentity>> GetDevicesAndModulesInTargetScopeAsync(string targetDeviceId);

        Task<Option<string>> GetAuthChain(string id);

        void InitiateCacheRefresh();

        Task RefreshServiceIdentities(IEnumerable<string> ids);

        Task RefreshServiceIdentity(string id);
    }
}
