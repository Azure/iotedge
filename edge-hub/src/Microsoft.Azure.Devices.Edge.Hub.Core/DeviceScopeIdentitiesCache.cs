// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public sealed class DeviceScopeIdentitiesCache : IDeviceScopeIdentitiesCache
    {
        readonly IServiceProxy serviceProxy;
        readonly IKeyValueStore<string, string> encryptedStore;
        readonly AsyncLock cacheLock = new AsyncLock();
        readonly IDictionary<string, StoredServiceIdentity> serviceIdentityCache;
        readonly Timer refreshCacheTimer;
        readonly TimeSpan refreshRate;
        Task refreshCacheTask;
        readonly object refreshCacheLock = new object();

        public event EventHandler<ServiceIdentity> ServiceIdentityUpdated;
        public event EventHandler<string> ServiceIdentityRemoved;

        DeviceScopeIdentitiesCache(IServiceProxy serviceProxy,
            IKeyValueStore<string, string> encryptedStorage,
            IDictionary<string, StoredServiceIdentity> initialCache,
            TimeSpan refreshRate)
        {
            this.serviceProxy = serviceProxy;
            this.encryptedStore = encryptedStorage;
            this.serviceIdentityCache = initialCache;
            this.refreshRate = refreshRate;
            this.refreshCacheTimer = new Timer(this.RefreshCache, null, TimeSpan.Zero, refreshRate);
        }

        public static async Task<DeviceScopeIdentitiesCache> Create(
            IServiceProxy serviceProxy,
            IKeyValueStore<string, string> encryptedStorage,
            TimeSpan refreshRate)
        {
            Preconditions.CheckNotNull(serviceProxy, nameof(serviceProxy));
            Preconditions.CheckNotNull(encryptedStorage, nameof(encryptedStorage));
            Preconditions.CheckArgument(refreshRate < TimeSpan.FromMinutes(5), "Refresh rate should be greater than once every 5 mins.");
            IDictionary<string, StoredServiceIdentity> cache = await ReadCacheFromStore(encryptedStorage);
            var deviceScopeIdentitiesCache = new DeviceScopeIdentitiesCache(serviceProxy, encryptedStorage, cache, refreshRate);
            Events.Created();
            return deviceScopeIdentitiesCache;
        }

        void RefreshCache(object state)
        {
            lock (this.refreshCacheLock)
            {
                if (this.refreshCacheTask == null || this.refreshCacheTask.IsCompleted)
                {
                    Events.InitializingRefreshTask(this.refreshRate);
                    this.refreshCacheTask = this.RefreshCache();
                }
            }
        }

        public async Task RefreshCache()
        {
            while (true)
            {
                try
                {
                    Events.StartingRefreshCycle();
                    IEnumerable<string> currentCacheIds = new List<string>(this.serviceIdentityCache.Keys);
                    IServiceIdentitiesIterator iterator = this.serviceProxy.GetServiceIdentitiesIterator();
                    while (iterator.HasNext)
                    {
                        IEnumerable<ServiceIdentity> batch = await iterator.GetNext();
                        foreach (ServiceIdentity serviceIdentity in batch)
                        {
                            await this.HandleNewServiceIdentity(serviceIdentity);
                        }
                    }

                    // Diff and update
                    IEnumerable<string> removedIds = currentCacheIds.Except(this.serviceIdentityCache.Keys);
                    await Task.WhenAll(removedIds.Select(id => this.HandleNoServiceIdentity(id)));
                }
                catch (Exception e)
                {
                    Events.ErrorInRefreshCycle(e);
                }

                Events.DoneRefreshCycle(this.refreshRate);
                await Task.Delay(this.refreshRate);
            }
        }

        public async Task RefreshCache(string deviceId)
        {
            try
            {
                Option<ServiceIdentity> serviceIdentity = await this.serviceProxy.GetServiceIdentity(deviceId);
                await serviceIdentity
                    .Map(this.HandleNewServiceIdentity)
                    .GetOrElse(this.HandleNoServiceIdentity(deviceId));
            }
            catch (Exception e)
            {
                Events.ErrorRefreshingCache(e, deviceId);
            }
        }

        public async Task RefreshCache(string deviceId, string moduleId)
        {
            try
            {
                Option<ServiceIdentity> serviceIdentity = await this.serviceProxy.GetServiceIdentity(deviceId, moduleId);
                await serviceIdentity
                    .Map(this.HandleNewServiceIdentity)
                    .GetOrElse(this.HandleNoServiceIdentity($"{deviceId}/{moduleId}"));
            }
            catch (Exception e)
            {
                Events.ErrorRefreshingCache(e, $"{deviceId}/{moduleId}");
            }
        }

        public async Task RefreshCache(IEnumerable<string> deviceIds)
        {
            List<string> deviceIdsList = Preconditions.CheckNotNull(deviceIds, nameof(deviceIds)).ToList();
            foreach (string deviceId in deviceIdsList)
            {
                await this.RefreshCache(deviceId);
            }
        }

        public async Task<Option<ServiceIdentity>> GetServiceIdentity(string id)
        {
            using (await this.cacheLock.LockAsync())
            {
                if (this.serviceIdentityCache.TryGetValue(id, out StoredServiceIdentity storedServiceIdentity))
                {
                    return storedServiceIdentity.ServiceIdentity;
                }

                return Option.None<ServiceIdentity>();
            }
        }

        async Task HandleNoServiceIdentity(string id)
        {
            using (await this.cacheLock.LockAsync())
            {
                var storedServiceIdentity = new StoredServiceIdentity(id);
                this.serviceIdentityCache[id] = storedServiceIdentity;
                await this.SaveServiceIdentityToStore(id, storedServiceIdentity);

                // Remove device if connected
                this.ServiceIdentityRemoved?.Invoke(this, id);
            }
        }

        async Task HandleNewServiceIdentity(ServiceIdentity serviceIdentity)
        {
            using (await this.cacheLock.LockAsync())
            {
                bool hasUpdated = !await this.CompareWithCacheValue(serviceIdentity);
                var storedServiceIdentity = new StoredServiceIdentity(serviceIdentity);
                this.serviceIdentityCache[serviceIdentity.Id] = storedServiceIdentity;
                await this.SaveServiceIdentityToStore(serviceIdentity.Id, storedServiceIdentity);

                if (hasUpdated)
                {
                    this.ServiceIdentityUpdated?.Invoke(this, serviceIdentity);
                }
            }
        }

        async Task<bool> CompareWithCacheValue(ServiceIdentity serviceIdentity)
        {
            using (await this.cacheLock.LockAsync())
            {
                if (this.serviceIdentityCache.TryGetValue(serviceIdentity.Id, out StoredServiceIdentity currentStoredServiceIdentity))
                {
                    return currentStoredServiceIdentity.ServiceIdentity
                        .Map(s => s.Equals(serviceIdentity))
                        .GetOrElse(false);
                }
            }

            return false;
        }

        async Task SaveServiceIdentityToStore(string id, StoredServiceIdentity storedServiceIdentity)
        {
            string serviceIdentityString = JsonConvert.SerializeObject(storedServiceIdentity);
            await this.encryptedStore.Put(id, serviceIdentityString);
        }

        static async Task<IDictionary<string, StoredServiceIdentity>> ReadCacheFromStore(IKeyValueStore<string, string> encryptedStore)
        {
            IDictionary<string, StoredServiceIdentity> cache = new Dictionary<string, StoredServiceIdentity>();
            await encryptedStore.IterateBatch(
                int.MaxValue,
                (key, value) =>
                {
                    cache.Add(key, JsonConvert.DeserializeObject<StoredServiceIdentity>(value));
                    return Task.CompletedTask;
                });
            return cache;
        }

        public void Dispose()
        {
            this.encryptedStore?.Dispose();
            this.refreshCacheTimer?.Dispose();
            this.refreshCacheTask?.Dispose();
        }

        class StoredServiceIdentity
        {
            public StoredServiceIdentity(ServiceIdentity serviceIdentity)
                : this(Preconditions.CheckNotNull(serviceIdentity, nameof(serviceIdentity)).Id, serviceIdentity, DateTime.UtcNow)
            {
            }

            public StoredServiceIdentity(string id)
                : this(Preconditions.CheckNotNull(id, nameof(id)), null, DateTime.UtcNow)
            { }

            [JsonConstructor]
            public StoredServiceIdentity(string id, ServiceIdentity serviceIdentity, DateTime timestamp)
            {
                this.ServiceIdentity = Option.Maybe(serviceIdentity);
                this.Id = Preconditions.CheckNotNull(id);
                this.Timestamp = timestamp;
            }

            public Option<ServiceIdentity> ServiceIdentity { get; }

            public string Id { get; }

            public DateTime Timestamp { get; }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<IDeviceScopeIdentitiesCache>();
            const int IdStart = HubCoreEventIds.DeviceScopeIdentitiesCache;

            enum EventIds
            {
                InitializingRefreshTask = IdStart,
                Created,
                ErrorInRefresh,
                StartingCycle,
                DoneCycle
            }

            internal static void InitializingRefreshTask(TimeSpan refreshRate) =>
                Log.LogDebug((int)EventIds.InitializingRefreshTask, $"Initializing device scope identities cache refresh task to run every {refreshRate.TotalMinutes} minutes.");

            public static void Created() =>
                Log.LogDebug((int)EventIds.Created, "Created device scope identities cache");

            public static void ErrorInRefreshCycle(Exception exception) =>
                Log.LogWarning((int)EventIds.ErrorInRefresh, exception, "Error while refreshing the device scope identities cache");

            public static void StartingRefreshCycle() =>
                Log.LogDebug((int)EventIds.StartingCycle, "Starting refresh of device scope identities cache");

            public static void DoneRefreshCycle(TimeSpan refreshRate) =>
                Log.LogDebug((int)EventIds.DoneCycle, $"Done refreshing device scope identities cache. Waiting for {refreshRate.TotalMinutes} minutes.");

            public static void ErrorRefreshingCache(Exception exception, string deviceId)
            {
                throw new NotImplementedException();
            }
        }
    }
}
