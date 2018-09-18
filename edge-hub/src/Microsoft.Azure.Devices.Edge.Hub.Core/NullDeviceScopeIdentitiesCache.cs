// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullDeviceScopeIdentitiesCache : IDeviceScopeIdentitiesCache
    {
        public Task<Option<ServiceIdentity>> GetServiceIdentity(string id, bool refreshCache = false)
            => Task.FromResult(Option.None<ServiceIdentity>());

        public Task<Option<ServiceIdentity>> GetServiceIdentity(string deviceId, string moduleId, bool refreshCache = false)
            => Task.FromResult(Option.None<ServiceIdentity>());

        public void InitiateCacheRefresh()
        {
        }

        public Task RefreshServiceIdentities(IEnumerable<string> deviceIds) => Task.CompletedTask;

        public Task RefreshServiceIdentity(string deviceId) => Task.CompletedTask;

        public Task RefreshServiceIdentity(string deviceId, string moduleId) => Task.CompletedTask;

        public event EventHandler<ServiceIdentity> ServiceIdentityUpdated { add { } remove { } }

        public event EventHandler<string> ServiceIdentityRemoved { add { } remove { } }
    }
}
