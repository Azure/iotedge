// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    public class AllFitDeviceScopeIdentitiesCache : IDeviceScopeIdentitiesCache
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

        public Task<Option<ServiceIdentity>> GetServiceIdentity(string id, bool refreshIfNotExists = false)
            => Task.FromResult(Option.Some(new ServiceIdentity(id, "123", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.None), ServiceIdentityStatus.Enabled)));

        public Task<Option<ServiceIdentity>> GetServiceIdentity(string deviceId, string moduleId, bool refreshIfNotExists = false)
            => Task.FromResult(Option.Some(new ServiceIdentity($"{deviceId}/{moduleId}", "123", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.None), ServiceIdentityStatus.Enabled)));

        public void InitiateCacheRefresh()
        {
        }

        public Task RefreshServiceIdentities(IEnumerable<string> deviceIds) => Task.CompletedTask;
        public Task RefreshServiceIdentity(string deviceId) => Task.CompletedTask;
        public Task RefreshServiceIdentity(string deviceId, string moduleId) => Task.CompletedTask;
    }
}
