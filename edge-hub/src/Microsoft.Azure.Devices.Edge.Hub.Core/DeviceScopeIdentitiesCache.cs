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
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public sealed class DeviceScopeIdentitiesCache : IDeviceScopeIdentitiesCache
    {
        readonly IServiceProxy serviceProxy;
        readonly IKeyValueStore<string, string> encryptedStore;
        readonly AsyncLockProvider<string> cacheLockProvider;
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
            TimeSpan refreshRate,
            int keyShards = 12)
        {
            this.serviceProxy = serviceProxy;
            this.encryptedStore = encryptedStorage;
            this.serviceIdentityCache = initialCache;
            this.refreshRate = refreshRate;
            this.refreshCacheTimer = new Timer(this.RefreshCache, null, TimeSpan.Zero, refreshRate);
            this.cacheLockProvider = new AsyncLockProvider<string>(keyShards);
        }

        public static async Task<DeviceScopeIdentitiesCache> Create(
            IServiceProxy serviceProxy,
            IKeyValueStore<string, string> encryptedStorage,
            TimeSpan refreshRate)
        {
            Preconditions.CheckNotNull(serviceProxy, nameof(serviceProxy));
            Preconditions.CheckNotNull(encryptedStorage, nameof(encryptedStorage));
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
                    var currentCacheIds = new List<string>();
                    IServiceIdentitiesIterator iterator = this.serviceProxy.GetServiceIdentitiesIterator();
                    while (iterator.HasNext)
                    {
                        IEnumerable<ServiceIdentity> batch = await iterator.GetNext();
                        foreach (ServiceIdentity serviceIdentity in batch)
                        {
                            try
                            {
                                await this.HandleNewServiceIdentity(serviceIdentity);
                                currentCacheIds.Add(serviceIdentity.Id);
                            }
                            catch (Exception e)
                            {
                                Events.ErrorProcessing(serviceIdentity, e);
                            }
                        }
                    }

                    // Diff and update
                    List<string> removedIds = this.serviceIdentityCache.Keys.Except(currentCacheIds).ToList();
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

        public async Task RefreshServiceIdentity(string deviceId)
        {
            try
            {
                Option<ServiceIdentity> serviceIdentity = await this.serviceProxy.GetServiceIdentity(deviceId);
                await serviceIdentity
                    .Map(s => this.HandleNewServiceIdentity(s))
                    .GetOrElse(() => this.HandleNoServiceIdentity(deviceId));
            }
            catch (Exception e)
            {
                Events.ErrorRefreshingCache(e, deviceId);
            }
        }

        public async Task RefreshServiceIdentity(string deviceId, string moduleId)
        {
            try
            {
                Option<ServiceIdentity> serviceIdentity = await this.serviceProxy.GetServiceIdentity(deviceId, moduleId);
                await serviceIdentity
                    .Map(s => this.HandleNewServiceIdentity(s))
                    .GetOrElse(() => this.HandleNoServiceIdentity($"{deviceId}/{moduleId}"));
            }
            catch (Exception e)
            {
                Events.ErrorRefreshingCache(e, $"{deviceId}/{moduleId}");
            }
        }

        public async Task RefreshServiceIdentities(IEnumerable<string> deviceIds)
        {
            List<string> deviceIdsList = Preconditions.CheckNotNull(deviceIds, nameof(deviceIds)).ToList();
            foreach (string deviceId in deviceIdsList)
            {
                await this.RefreshServiceIdentity(deviceId);
            }
        }

        public Task<Option<ServiceIdentity>> GetServiceIdentity(string deviceId, string moduleId)
        {
            Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            return this.GetServiceIdentity($"{deviceId}/{moduleId}");
        }

        public async Task<Option<ServiceIdentity>> GetServiceIdentity(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            using (await this.cacheLockProvider.GetLock(id).LockAsync())
            {
                return this.serviceIdentityCache.TryGetValue(id, out StoredServiceIdentity storedServiceIdentity)
                    ? storedServiceIdentity.ServiceIdentity
                    : Option.None<ServiceIdentity>();
            }
        }

        async Task HandleNoServiceIdentity(string id)
        {
            using (await this.cacheLockProvider.GetLock(id).LockAsync())
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
            using (await this.cacheLockProvider.GetLock(serviceIdentity.Id).LockAsync())
            {
                bool hasUpdated = this.serviceIdentityCache.TryGetValue(serviceIdentity.Id, out StoredServiceIdentity currentStoredServiceIdentity)
                    && currentStoredServiceIdentity.ServiceIdentity
                        .Map(s => !s.Equals(serviceIdentity))
                        .GetOrElse(false);
                var storedServiceIdentity = new StoredServiceIdentity(serviceIdentity);
                this.serviceIdentityCache[serviceIdentity.Id] = storedServiceIdentity;
                await this.SaveServiceIdentityToStore(serviceIdentity.Id, storedServiceIdentity);

                if (hasUpdated)
                {
                    this.ServiceIdentityUpdated?.Invoke(this, serviceIdentity);
                }
            }
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

        internal class StoredServiceIdentity
        {
            public StoredServiceIdentity(ServiceIdentity serviceIdentity)
                : this(Preconditions.CheckNotNull(serviceIdentity, nameof(serviceIdentity)).Id, serviceIdentity, DateTime.UtcNow)
            {
            }

            public StoredServiceIdentity(string id)
                : this(Preconditions.CheckNotNull(id, nameof(id)), null, DateTime.UtcNow)
            { }

            [JsonConstructor]
            StoredServiceIdentity(string id, ServiceIdentity serviceIdentity, DateTime timestamp)
            {
                this.ServiceIdentity = Option.Maybe(serviceIdentity);
                this.Id = Preconditions.CheckNotNull(id);
                this.Timestamp = timestamp;
            }

            [JsonProperty("serviceIdentity")]
            [JsonConverter(typeof(OptionConverter<ServiceIdentity>))]
            public Option<ServiceIdentity> ServiceIdentity { get; }

            [JsonProperty("id")]
            public string Id { get; }

            [JsonProperty("timestamp")]
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
                Log.LogWarning((int)EventIds.ErrorInRefresh, exception, $"Error while refreshing the service identity for {deviceId}");
            }

            public static void ErrorProcessing(ServiceIdentity serviceIdentity, Exception exception)
            {
                string id = serviceIdentity?.Id ?? "unknown";
                Log.LogWarning((int)EventIds.ErrorInRefresh, exception, $"Error while processing the service identity for {id}");
            }
        }
    }
}
