// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Newtonsoft.Json;

    public interface ISecurityScopeStore : IDisposable
    {
        Task<Option<ServiceIdentity>> GetServiceIdentity(string id);
    }

    public sealed class SecurityScopeStore : ISecurityScopeStore
    {
        readonly IServiceProxy serviceProxy;
        readonly IEncryptedStore<string, string> encryptedStore;
        readonly AsyncLock asyncLock = new AsyncLock();
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
            using (await this.asyncLock.LockAsync())
            {
                IDictionary<string, ServiceIdentity> cache = await this.ReadCacheFromStore();
                try
                {
                    ISecurityScopeIdentitiesIterator iterator = this.serviceProxy.GetSecurityScopeIdentitiesIterator();
                    while (true)
                    {
                        IEnumerable<ServiceIdentity> batch = await iterator.GetNext();
                        if (!batch.Any())
                        {
                            break;
                        }

                        foreach (ServiceIdentity serviceIdentity in batch)
                        {
                            string id = serviceIdentity.ModuleId != null ? $"{serviceIdentity.DeviceId}/{serviceIdentity.ModuleId}" : serviceIdentity.DeviceId;
                            cache.Add(id, serviceIdentity);
                            await this.SaveServiceIdentityToStore(id, serviceIdentity);
                        }
                    }
                }
                catch (Exception)
                {
                }
                this.serviceIdentityCache = cache;
            }
        }

        async Task SaveServiceIdentityToStore(string id, ServiceIdentity serviceIdentity)
        {
            string serviceIdentityString = JsonConvert.SerializeObject(serviceIdentity);
            await this.encryptedStore.Put(id, serviceIdentityString);
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
