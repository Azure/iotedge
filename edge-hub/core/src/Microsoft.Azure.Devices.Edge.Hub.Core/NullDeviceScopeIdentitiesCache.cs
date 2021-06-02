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
        public event EventHandler<string> ServiceIdentityRemoved
        {
            add { }
            remove { }
        }

        public event EventHandler<ServiceIdentity> ServiceIdentityUpdated
        {
            add { }
            remove { }
        }

        public event EventHandler<IList<string>> ServiceIdentitiesUpdated
        {
            add { }
            remove { }
        }

        public Task<Option<ServiceIdentity>> GetServiceIdentity(string id)
            => Task.FromResult(Option.None<ServiceIdentity>());

        public Task<Option<ServiceIdentity>> GetServiceIdentity(string deviceId, string moduleId, bool refreshIfNotExists = false)
            => Task.FromResult(Option.None<ServiceIdentity>());

        public Task<string> VerifyServiceIdentityAuthChainState(string id, bool isNestedEdgeEnabled, bool __ = false) => Task.FromResult(id);

        public Task<Option<string>> GetAuthChain(string _) => Task.FromResult(Option.None<string>());

        public Task<IList<string>> GetAllIds()
        {
            IList<string> list = new List<string>();
            return Task.FromResult(list);
        }

        public Task<IList<ServiceIdentity>> GetDevicesAndModulesInTargetScopeAsync(string _)
        {
            IList<ServiceIdentity> list = new List<ServiceIdentity>();
            return Task.FromResult(list);
        }

        public void InitiateCacheRefresh()
        {
        }

        public Task WaitForCacheRefresh(TimeSpan _) => Task.CompletedTask;

        public Task RefreshServiceIdentities(IEnumerable<string> deviceIds) => Task.CompletedTask;

        public Task RefreshServiceIdentity(string deviceId) => Task.CompletedTask;

        public Task RefreshServiceIdentityOnBehalfOf(string refreshTarget, string onBehalfOfDevice) => Task.CompletedTask;

        public Task RefreshAuthChain(string authChain) => Task.CompletedTask;
    }
}
