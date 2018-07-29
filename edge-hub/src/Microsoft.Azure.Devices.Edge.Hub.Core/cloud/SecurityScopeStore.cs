// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public interface ISecurityScopeStore : IDisposable
    {
        Task<Option<ServiceIdentity>> GetServiceIdentity(string id);
    }

    public sealed class SecurityScopeStore : ISecurityScopeStore
    {
        readonly IServiceProxy serviceProxy;
        readonly IEncryptedStore<string, string> encryptedStore;

        IDictionary<string, ServiceIdentity> serviceIdentityCache;
        readonly Timer refreshCacheTimer;
        Task refreshCacheTask;

        public SecurityScopeStore(IServiceProxy serviceProxy, IEncryptedStore<string, string> encryptedStorage)
        {
            this.serviceProxy = serviceProxy;
            this.encryptedStore = encryptedStorage;
            this.serviceIdentityCache = new Dictionary<string, ServiceIdentity>();
            this.refreshCacheTimer = new Timer(this.RefreshCache, null, TimeSpan.Zero, TimeSpan.FromHours(1));
        }

        void RefreshCache(object state)
        {
            if (this.refreshCacheTask == null || this.refreshCacheTask.IsCompleted)
            {
                this.refreshCacheTask = this.RefreshCache();                
            }
        }

        async Task RefreshCache()
        {
            IDictionary<string, ServiceIdentity> cache = new Dictionary<string, ServiceIdentity>();
            try
            {
                IEnumerable<string> deviceIds = await this.serviceProxy.GetDevicesInScope();
                foreach (string deviceId in deviceIds)
                {
                    ServiceIdentity serviceIdentity = await this.serviceProxy.GetDevice(deviceId);
                    cache.Add(deviceId, serviceIdentity);
                    if (serviceIdentity.IsEdgeDevice)
                    {
                        IEnumerable<ServiceIdentity> serviceIdentities = await this.serviceProxy.GetModulesOnDevice(deviceId);
                        foreach (ServiceIdentity moduleIdentity in serviceIdentities)
                        {
                            cache.Add($"{moduleIdentity.DeviceId}/{moduleIdentity.ModuleId}", moduleIdentity);
                        }
                    }
                }
                await this.SaveCacheToStore(cache);
            }
            catch (Exception)
            {
                cache = await this.ReadCacheFromStore();
            }
            this.serviceIdentityCache = cache;
        }

        async Task SaveCacheToStore(IDictionary<string, ServiceIdentity> cache)
        {
            foreach (KeyValuePair<string, ServiceIdentity> serviceIdentity in cache)
            {
                string cacheString = JsonConvert.SerializeObject(cache);
                await this.encryptedStore.Put(serviceIdentity.Key, cacheString);
            }
        }

        async Task<IDictionary<string, ServiceIdentity>> ReadCacheFromStore()
        {
            IDictionary<string, ServiceIdentity> cache = new Dictionary<string, ServiceIdentity>();
            await this.encryptedStore.IterateBatch(
                int.MaxValue,
                (key, value) =>
                {
                    cache.Add(key, JsonConvert.DeserializeObject<ServiceIdentity>(value));
                    return Task.CompletedTask;
                });
            return cache;
        }

        public Task<Option<ServiceIdentity>> GetServiceIdentity(string id)
        {
            if (this.serviceIdentityCache.TryGetValue(id, out ServiceIdentity serviceIdentity))
            {
                return Task.FromResult(Option.Some(serviceIdentity));
            }
            return Task.FromResult(Option.None<ServiceIdentity>());
        }

        public void Dispose()
        {
            this.encryptedStore?.Dispose();
            this.refreshCacheTimer?.Dispose();
            this.refreshCacheTask?.Dispose();
        }
    }
}
