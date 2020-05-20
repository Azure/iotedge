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
    using Nito.AsyncEx;
    using AsyncLock = Microsoft.Azure.Devices.Edge.Util.Concurrency.AsyncLock;

    public sealed class DeviceScopeIdentitiesCache : IDeviceScopeIdentitiesCache
    {
        readonly IServiceProxy serviceProxy;
        readonly IKeyValueStore<string, string> encryptedStore;
        readonly AsyncLock cacheLock = new AsyncLock();
        readonly ServiceIdentityTree serviceIdentityTree;
        readonly Timer refreshCacheTimer;
        readonly TimeSpan refreshRate;
        readonly AsyncAutoResetEvent refreshCacheSignal = new AsyncAutoResetEvent();
        readonly object refreshCacheLock = new object();

        Task refreshCacheTask;

        DeviceScopeIdentitiesCache(
            string edgeDeviceId,
            IServiceProxy serviceProxy,
            IKeyValueStore<string, string> encryptedStorage,
            IDictionary<string, StoredServiceIdentity> initialCache,
            TimeSpan refreshRate)
        {
            this.serviceProxy = serviceProxy;
            this.encryptedStore = encryptedStorage;
            this.refreshRate = refreshRate;
            this.refreshCacheTimer = new Timer(this.RefreshCache, null, TimeSpan.Zero, refreshRate);

            // Populate the ServiceIdentityTree
            this.serviceIdentityTree = new ServiceIdentityTree(edgeDeviceId);
            this.serviceIdentityTree.ServiceIdentityRemoved += this.HandleServiceIdentityRemoved;
            foreach (KeyValuePair<string, StoredServiceIdentity> kvp in initialCache)
            {
                kvp.Value.ServiceIdentity.ForEach(serviceIdentity => this.serviceIdentityTree.InsertOrUpdate(serviceIdentity));
            }
        }

        public event EventHandler<string> ServiceIdentityRemoved;

        public event EventHandler<ServiceIdentity> ServiceIdentityUpdated;

        public static async Task<DeviceScopeIdentitiesCache> Create(
            string edgeDeviceId,
            IServiceProxy serviceProxy,
            IKeyValueStore<string, string> encryptedStorage,
            TimeSpan refreshRate)
        {
            Preconditions.CheckNotNull(serviceProxy, nameof(serviceProxy));
            Preconditions.CheckNotNull(encryptedStorage, nameof(encryptedStorage));
            Preconditions.CheckNonWhiteSpace(edgeDeviceId, nameof(edgeDeviceId));
            IDictionary<string, StoredServiceIdentity> cache = await ReadCacheFromStore(encryptedStorage);
            var deviceScopeIdentitiesCache = new DeviceScopeIdentitiesCache(edgeDeviceId, serviceProxy, encryptedStorage, cache, refreshRate);
            Events.Created();
            return deviceScopeIdentitiesCache;
        }

        public void InitiateCacheRefresh()
        {
            Events.ReceivedRequestToRefreshCache();
            this.refreshCacheSignal.Set();
        }

        public async Task RefreshServiceIdentity(string id)
        {
            try
            {
                Events.RefreshingServiceIdentity(id);
                Option<ServiceIdentity> serviceIdentity = await this.GetServiceIdentityFromService(id);
                await serviceIdentity
                    .Map(s => this.HandleNewServiceIdentity(s))
                    .GetOrElse(() => this.HandleNoServiceIdentity(id));
            }
            catch (Exception e)
            {
                Events.ErrorRefreshingCache(e, id);
            }
        }

        public async Task RefreshServiceIdentities(IEnumerable<string> ids)
        {
            List<string> idList = Preconditions.CheckNotNull(ids, nameof(ids)).ToList();
            foreach (string id in idList)
            {
                await this.RefreshServiceIdentity(id);
            }
        }

        public async Task<Option<ServiceIdentity>> GetServiceIdentity(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Events.GettingServiceIdentity(id);
            return await this.GetServiceIdentityInternal(id);
        }

        public void Dispose()
        {
            this.encryptedStore?.Dispose();
            this.refreshCacheTimer?.Dispose();
            this.refreshCacheTask?.Dispose();
        }

        internal Task<Option<ServiceIdentity>> GetServiceIdentityFromService(string id)
        {
            // If it is a module id, it will have the format "deviceId/moduleId"
            string[] parts = id.Split('/');
            if (parts.Length == 2)
            {
                return this.serviceProxy.GetServiceIdentity(parts[0], parts[1]);
            }
            else
            {
                return this.serviceProxy.GetServiceIdentity(id);
            }
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

        async Task RefreshCache()
        {
            while (true)
            {
                try
                {
                    Events.StartingRefreshCycle();
                    var currentCacheIds = new List<string>();
                    IServiceIdentitiesIterator iterator = this.serviceProxy.GetNestedServiceIdentitiesIterator();
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
                    List<string> removedIds = this.serviceIdentityTree
                        .GetAllIds()
                        .Except(currentCacheIds)
                        .ToList();
                    await Task.WhenAll(removedIds.Select(id => this.HandleNoServiceIdentity(id)));
                }
                catch (Exception e)
                {
                    Events.ErrorInRefreshCycle(e);
                }

                Events.DoneRefreshCycle(this.refreshRate);
                await this.IsReady();
            }
        }

        async Task IsReady()
        {
            Task refreshCacheSignalTask = this.refreshCacheSignal.WaitAsync();
            Task sleepTask = Task.Delay(this.refreshRate);
            Task task = await Task.WhenAny(refreshCacheSignalTask, sleepTask);
            if (task == refreshCacheSignalTask)
            {
                Events.RefreshSignalled();
            }
            else
            {
                Events.RefreshSleepCompleted();
            }
        }

        async Task<Option<ServiceIdentity>> GetServiceIdentityInternal(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            using (await this.cacheLock.LockAsync())
            {
                return this.serviceIdentityTree.Get(id);
            }
        }

        async Task HandleNoServiceIdentity(string id)
        {
            using (await this.cacheLock.LockAsync())
            {
                bool hasValidServiceIdentity = this.serviceIdentityTree.Get(id)
                    .Filter(s => s.Status == ServiceIdentityStatus.Enabled)
                    .HasValue;

                // Remove the target identity and all its children from the tree,
                // this will invoke a callback for each deleted identity, so we
                // can also delete them from storage.
                this.serviceIdentityTree.Remove(id);
                Events.NotInScope(id);

                if (hasValidServiceIdentity)
                {
                    // Remove device if connected, if service identity existed and then was removed.
                    this.ServiceIdentityRemoved?.Invoke(this, id);
                }
            }
        }

        async Task HandleNewServiceIdentity(ServiceIdentity serviceIdentity)
        {
            using (await this.cacheLock.LockAsync())
            {
                Option<ServiceIdentity> existing = this.serviceIdentityTree.Get(serviceIdentity.Id);
                bool hasUpdated = existing.HasValue && !existing.Contains(serviceIdentity);

                this.serviceIdentityTree.InsertOrUpdate(serviceIdentity);
                await this.SaveServiceIdentityToStore(serviceIdentity.Id, new StoredServiceIdentity(serviceIdentity));
                Events.AddInScope(serviceIdentity.Id);

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

        async void HandleServiceIdentityRemoved(object sender, string id)
        {
            await this.encryptedStore.Remove(id);
        }

        internal class StoredServiceIdentity
        {
            public StoredServiceIdentity(ServiceIdentity serviceIdentity)
                : this(Preconditions.CheckNotNull(serviceIdentity, nameof(serviceIdentity)).Id, serviceIdentity, DateTime.UtcNow)
            {
            }

            public StoredServiceIdentity(string id)
                : this(Preconditions.CheckNotNull(id, nameof(id)), null, DateTime.UtcNow)
            {
            }

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
            const int IdStart = HubCoreEventIds.DeviceScopeIdentitiesCache;
            static readonly ILogger Log = Logger.Factory.CreateLogger<IDeviceScopeIdentitiesCache>();

            enum EventIds
            {
                InitializingRefreshTask = IdStart,
                Created,
                ErrorInRefresh,
                StartingCycle,
                DoneCycle,
                ReceivedRequestToRefreshCache,
                RefreshSleepCompleted,
                RefreshSignalled,
                NotInScope,
                AddInScope,
                RefreshingServiceIdentity,
                GettingServiceIdentity
            }

            public static void Created() =>
                Log.LogInformation((int)EventIds.Created, "Created device scope identities cache");

            public static void ErrorInRefreshCycle(Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorInRefresh, "Encountered an error while refreshing the device scope identities cache. Will retry the operation in some time...");
                Log.LogDebug((int)EventIds.ErrorInRefresh, exception, "Error details while refreshing the device scope identities cache");
            }

            public static void StartingRefreshCycle() =>
                Log.LogInformation((int)EventIds.StartingCycle, "Starting refresh of device scope identities cache");

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

            public static void ReceivedRequestToRefreshCache() =>
                Log.LogDebug((int)EventIds.ReceivedRequestToRefreshCache, "Received request to refresh cache.");

            public static void RefreshSignalled() =>
                Log.LogDebug((int)EventIds.RefreshSignalled, "Device scope identities refresh is ready because a refresh was signalled.");

            public static void RefreshSleepCompleted() =>
                Log.LogDebug((int)EventIds.RefreshSleepCompleted, "Device scope identities refresh is ready because the wait period is over.");

            public static void NotInScope(string id) =>
                Log.LogDebug((int)EventIds.NotInScope, $"{id} is not in device scope, removing from cache.");

            public static void AddInScope(string id) =>
                Log.LogDebug((int)EventIds.AddInScope, $"{id} is in device scope, adding to cache.");

            public static void GettingServiceIdentity(string id) =>
                Log.LogDebug((int)EventIds.GettingServiceIdentity, $"Getting service identity for {id}");

            public static void RefreshingServiceIdentity(string id) =>
                Log.LogDebug((int)EventIds.RefreshingServiceIdentity, $"Refreshing service identity for {id}");

            internal static void InitializingRefreshTask(TimeSpan refreshRate) =>
                Log.LogDebug((int)EventIds.InitializingRefreshTask, $"Initializing device scope identities cache refresh task to run every {refreshRate.TotalMinutes} minutes.");
        }
    }
}
