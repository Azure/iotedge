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

        event EventHandler<IList<string>> ServiceIdentitiesUpdated;

        Task<Option<ServiceIdentity>> GetServiceIdentity(string id);

        Task<string> VerifyServiceIdentityAuthChainState(string id, bool isNestedEdgeEnabled, bool refreshCachedIdentity);

        Task<IList<ServiceIdentity>> GetDevicesAndModulesInTargetScopeAsync(string targetDeviceId);

        Task<Option<string>> GetAuthChain(string id);

        Task<IList<string>> GetAllIds();

        void InitiateCacheRefresh();

        Task WaitForCacheRefresh(TimeSpan timeout);

        Task RefreshServiceIdentities(IEnumerable<string> ids);

        Task RefreshServiceIdentity(string id);

        Task RefreshServiceIdentityOnBehalfOf(string refreshTarget, string onBehalfOfDevice);

        Task RefreshAuthChain(string authChain);
    }
}
