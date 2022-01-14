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

        public Task<Option<ServiceIdentity>> GetServiceIdentity(string _, bool __ = false)
            => Task.FromResult(Option.None<ServiceIdentity>());

        public Task<Option<ServiceIdentity>> GetServiceIdentity(string _, string __, bool ___ = false)
            => Task.FromResult(Option.None<ServiceIdentity>());

        public void InitiateCacheRefresh()
        {
        }

        public bool VerifyDeviceIdentityStore()
        {
            return true;
        }

        public Task RefreshServiceIdentities(IEnumerable<string> _) => Task.CompletedTask;

        public Task RefreshServiceIdentity(string _) => Task.CompletedTask;

        public Task RefreshServiceIdentity(string _, string __) => Task.CompletedTask;

        public Task VerifyServiceIdentityState(string _, bool __ = false) => throw new DeviceInvalidStateException("Device identity not found in cache.");
    }
}
