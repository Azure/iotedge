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
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Nito.AsyncEx;

    public sealed class DeviceScopeIdentitiesCache : IDeviceScopeIdentitiesCache
    {
        static readonly TimeSpan defaultInitializationRefreshDelay = TimeSpan.FromSeconds(60);

        readonly string edgeDeviceId;
        readonly IServiceProxy serviceProxy;
        readonly ServiceIdentityStore store;
        readonly IServiceIdentityHierarchy serviceIdentityHierarchy;
        readonly Timer refreshCacheTimer;
        readonly TimeSpan periodicRefreshRate;
        readonly TimeSpan refreshDelay;
        readonly TimeSpan initializationRefreshDelay;
        readonly AsyncManualResetEvent refreshCacheSignal = new AsyncManualResetEvent(false);
        readonly AsyncManualResetEvent refreshCacheCompleteSignal = new AsyncManualResetEvent(false);
        readonly object refreshCacheLock = new object();
        readonly AtomicBoolean isInitialized = new AtomicBoolean(false);

        Task refreshCacheTask;
        DateTime cacheLastRefreshTime;
        Dictionary<string, DateTime> identitiesLastRefreshTime;

        DeviceScopeIdentitiesCache(
            IServiceIdentityHierarchy serviceIdentityHierarchy,
            IServiceProxy serviceProxy,
            ServiceIdentityStore identityStore,
            TimeSpan periodicRefreshRate,
            TimeSpan refreshDelay,
            TimeSpan initializationRefreshDelay,
            bool initializedFromCache)
        {
            this.serviceIdentityHierarchy = serviceIdentityHierarchy;
            this.edgeDeviceId = serviceIdentityHierarchy.GetActorDeviceId();
            this.serviceProxy = serviceProxy;
            this.store = identityStore;
            this.periodicRefreshRate = periodicRefreshRate;
            this.refreshDelay = refreshDelay;
            this.initializationRefreshDelay = initializationRefreshDelay;
            this.identitiesLastRefreshTime = new Dictionary<string, DateTime>();
            this.cacheLastRefreshTime = DateTime.MinValue;

            this.isInitialized.Set(initializedFromCache);

            // Kick off the initial refresh after we processed all the stored identities
            this.refreshCacheTimer = new Timer(this.RefreshCache, null, TimeSpan.Zero, this.periodicRefreshRate);
        }

        public event EventHandler<string> ServiceIdentityRemoved;

        public event EventHandler<ServiceIdentity> ServiceIdentityUpdated;

        public event EventHandler<IList<string>> ServiceIdentitiesUpdated;

        public static Task<DeviceScopeIdentitiesCache> Create(
            IServiceIdentityHierarchy serviceIdentityHierarchy,
            IServiceProxy serviceProxy,
            IKeyValueStore<string, string> encryptedStorage,
            TimeSpan refreshRate,
            TimeSpan refreshDelay)
        {
            return Create(serviceIdentityHierarchy, serviceProxy, encryptedStorage, refreshRate, refreshDelay, defaultInitializationRefreshDelay);
        }

        public static async Task<DeviceScopeIdentitiesCache> Create(
           IServiceIdentityHierarchy serviceIdentityHierarchy,
           IServiceProxy serviceProxy,
           IKeyValueStore<string, string> encryptedStorage,
           TimeSpan refreshRate,
           TimeSpan refreshDelay,
           TimeSpan initializationRefreshDelay)
        {
            Preconditions.CheckNotNull(serviceProxy, nameof(serviceProxy));
            Preconditions.CheckNotNull(encryptedStorage, nameof(encryptedStorage));
            Preconditions.CheckNotNull(serviceIdentityHierarchy, nameof(serviceIdentityHierarchy));

            var identityStore = new ServiceIdentityStore(encryptedStorage);
            IDictionary<string, StoredServiceIdentity> cache = await identityStore.ReadCacheFromStore(encryptedStorage, serviceIdentityHierarchy.GetActorDeviceId());

            // Populate the serviceIdentityHierarchy
            foreach (KeyValuePair<string, StoredServiceIdentity> kvp in cache)
            {
                await kvp.Value.ServiceIdentity.ForEachAsync(serviceIdentity => serviceIdentityHierarchy.AddOrUpdate(serviceIdentity));
            }

            var deviceScopeIdentitiesCache = new DeviceScopeIdentitiesCache(serviceIdentityHierarchy, serviceProxy, identityStore, refreshRate, refreshDelay, initializationRefreshDelay, cache.Count > 0);

            Events.Created();
            return deviceScopeIdentitiesCache;
        }

        public void InitiateCacheRefresh()
        {
            Events.ReceivedRequestToRefreshCache();

            lock (this.refreshCacheLock)
            {
                DateTime now = DateTime.UtcNow;
                // Only refresh the cache if we haven't done so recently
                if (now - this.cacheLastRefreshTime > this.refreshDelay)
                {
                    this.refreshCacheCompleteSignal.Reset();
                    this.refreshCacheSignal.Set();
                }
                else
                {
                    Events.SkipRefreshCache(this.cacheLastRefreshTime, this.refreshDelay);

                    if (!this.refreshCacheSignal.IsSet)
                    {
                        // The background thread for refresh the cache is idle,
                        // in this case we need to signal completion right away
                        // or anyone calling WaitForCacheRefresh() will be stuck
                        this.refreshCacheCompleteSignal.Set();
                    }
                }
            }
        }

        public Task WaitForCacheRefresh(TimeSpan timeout) => this.refreshCacheCompleteSignal.WaitAsync(timeout);

        public async Task RefreshServiceIdentity(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            await this.RefreshServiceIdentityInternal(id, this.edgeDeviceId, true);
        }

        public async Task RefreshServiceIdentityOnBehalfOf(string refreshTarget, string onBehalfOfDevice)
        {
            Preconditions.CheckNonWhiteSpace(refreshTarget, nameof(refreshTarget));
            Preconditions.CheckNonWhiteSpace(onBehalfOfDevice, nameof(onBehalfOfDevice));
            await this.RefreshServiceIdentityInternal(refreshTarget, onBehalfOfDevice, true);
        }

        public Task RefreshServiceIdentities(IEnumerable<string> ids) => this.RefreshServiceIdentities(ids, this.edgeDeviceId);

        async Task RefreshServiceIdentities(IEnumerable<string> ids, string onBehalfOf)
        {
            List<string> idList = Preconditions.CheckNotNull(ids, nameof(ids)).ToList();
            foreach (string id in idList)
            {
                await this.RefreshServiceIdentityInternal(id, onBehalfOf, false);
            }

            this.ServiceIdentitiesUpdated?.Invoke(this, await this.GetAllIds());
        }

        async Task RefreshServiceIdentityInternal(string refreshTarget, string onBehalfOfDevice, bool invokeServiceIdentitiesUpdated)
        {
            try
            {
                if (await this.ShouldRefreshIdentity(refreshTarget))
                {
                    Events.RefreshingServiceIdentity(refreshTarget);
                    Option<ServiceIdentity> serviceIdentity = await this.GetServiceIdentityFromService(refreshTarget, onBehalfOfDevice);
                    await serviceIdentity
                        .Map(s => this.HandleNewServiceIdentity(s))
                        .GetOrElse(() => this.HandleNoServiceIdentity(refreshTarget));

                    // Update the timestamp for this identity so we
                    // don't repeatedly refresh the same identity
                    // in rapid succession.
                    this.identitiesLastRefreshTime[refreshTarget] = DateTime.UtcNow;

                    if (invokeServiceIdentitiesUpdated)
                    {
                        this.ServiceIdentitiesUpdated?.Invoke(this, await this.GetAllIds());
                    }
                }
                else
                {
                    Events.SkipRefreshServiceIdentity(refreshTarget, this.identitiesLastRefreshTime[refreshTarget], this.refreshDelay);
                }
            }
            catch (DeviceInvalidStateException ex)
            {
                Events.ErrorRefreshingCache(ex, refreshTarget, this.edgeDeviceId);

                await this.HandleNoServiceIdentity(refreshTarget);
                this.identitiesLastRefreshTime[refreshTarget] = DateTime.UtcNow;
            }
            catch (Exception e)
            {
                Events.ErrorRefreshingCache(e, refreshTarget, onBehalfOfDevice);
            }
        }

        public async Task RefreshAuthChain(string authChain)
        {
            Preconditions.CheckNonWhiteSpace(authChain, nameof(authChain));

            // Refresh each element in the auth-chain
            Events.RefreshingAuthChain(authChain);
            string[] ids = AuthChainHelpers.GetAuthChainIds(authChain);
            await this.RefreshServiceIdentities(ids, this.edgeDeviceId);
        }

        public async Task<Option<ServiceIdentity>> GetServiceIdentity(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Events.GettingServiceIdentity(id);
            return await this.GetServiceIdentityInternal(id);
        }

        public async Task<string> VerifyServiceIdentityAuthChainState(string id, bool isNestedEdgeEnabled, bool refreshCachedIdentity)
        {
            if (isNestedEdgeEnabled)
            {
                return await this.VerifyServiceIdentityAuthChainState(id, refreshCachedIdentity);
            }
            else
            {
                await this.VerifyServiceIdentityState(id, refreshCachedIdentity);
                return string.Empty;
            }
        }

        async Task VerifyServiceIdentityState(string id, bool refreshCachedIdentity = false)
        {
            if (refreshCachedIdentity)
            {
                await this.RefreshServiceIdentityInternal(id, this.edgeDeviceId, !refreshCachedIdentity);
            }

            Option<ServiceIdentity> serviceIdentity = await this.GetServiceIdentity(id);

            this.VerifyServiceIdentity(serviceIdentity.Expect(() =>
            {
                Events.VerifyServiceIdentityFailure(id, "Device is out of scope.");
                return new DeviceInvalidStateException("Device is out of scope.");
            }));
        }

        async Task<string> VerifyServiceIdentityAuthChainState(string id, bool refreshCachedIdentity = false)
        {
            if (refreshCachedIdentity)
            {
                Events.RefreshingServiceIdentity(id);

                var authChainTry = await this.serviceIdentityHierarchy.TryGetAuthChain(id);
                var onBehalfOfDeviceId = AuthChainHelpers.GetAuthParent(authChainTry.Ok());
                await this.RefreshServiceIdentityOnBehalfOf(id, onBehalfOfDeviceId.GetOrElse(this.edgeDeviceId));
            }

            string authChain = (await this.serviceIdentityHierarchy.TryGetAuthChain(id)).Value;

            Option<ServiceIdentity> serviceIdentity = await this.GetServiceIdentity(id);
            this.VerifyServiceIdentity(serviceIdentity.Expect(() =>
            {
                Events.VerifyServiceIdentityFailure(id, "Device is out of scope.");
                return new DeviceInvalidStateException("Device is out of scope.");
            }));

            return authChain;
        }

        void VerifyServiceIdentity(ServiceIdentity serviceIdentity)
        {
            if (serviceIdentity.Status != ServiceIdentityStatus.Enabled)
            {
                Events.VerifyServiceIdentityFailure(serviceIdentity.Id, "Device is disabled.");
                throw new DeviceInvalidStateException("Device is disabled.");
            }
        }

        public Task<Option<string>> GetAuthChain(string targetId)
        {
            Preconditions.CheckNonWhiteSpace(targetId, nameof(targetId));
            return this.serviceIdentityHierarchy.GetAuthChain(targetId);
        }

        public Task<IList<ServiceIdentity>> GetDevicesAndModulesInTargetScopeAsync(string targetDeviceId)
        {
            Preconditions.CheckNonWhiteSpace(targetDeviceId, nameof(targetDeviceId));
            return this.serviceIdentityHierarchy.GetImmediateChildren(targetDeviceId);
        }

        public async Task<IList<string>> GetAllIds() => await this.serviceIdentityHierarchy.GetAllIds();

        public void Dispose()
        {
            this.store?.Dispose();
            this.refreshCacheTimer?.Dispose();
            this.refreshCacheTask?.Dispose();
        }

        internal Task<Option<ServiceIdentity>> GetServiceIdentityFromService(string targetId, string onBehalfOfDevice)
        {
            // If it is a module id, it will have the format "deviceId/moduleId"
            string[] parts = targetId.Split('/');
            if (parts.Length == 2)
            {
                return this.serviceProxy.GetServiceIdentity(parts[0], parts[1], onBehalfOfDevice);
            }
            else
            {
                return this.serviceProxy.GetServiceIdentity(targetId, onBehalfOfDevice);
            }
        }

        async Task<bool> ShouldRefreshIdentity(string id)
        {
            if (!this.isInitialized.Get())
            {
                return false;
            }

            bool hasRefreshed = this.identitiesLastRefreshTime.TryGetValue(id, out DateTime lastRefreshTime);

            // Only refresh an identity if we haven't done so recently
            if (!hasRefreshed || DateTime.UtcNow - lastRefreshTime > this.refreshDelay)
            {
                return true;
            }

            // Identities can initially be created with no auth, and
            // have their auth type updated later. In this case we
            // must refresh the identity or we won't be able to auth
            // incoming OnBehalfOf connections for those identities.
            Option<ServiceIdentity> identityOption = await this.GetServiceIdentity(id);
            return identityOption.Match(id => id.Authentication.Type == ServiceAuthenticationType.None, () => true);
        }

        void RefreshCache(object state)
        {
            lock (this.refreshCacheLock)
            {
                if (this.refreshCacheTask == null || this.refreshCacheTask.IsCompleted)
                {
                    Events.InitializingRefreshTask(this.periodicRefreshRate);
                    this.refreshCacheTask = this.RefreshCache();
                }
            }
        }

        async Task RefreshCache()
        {
            while (true)
            {
                bool succeeded = await this.RefreshCacheInternal();
                if (this.ShouldRetryRefreshCache(succeeded))
                {
                    Events.RetryRefreshCycle(this.initializationRefreshDelay);
                    await this.IsReady(this.initializationRefreshDelay);
                }
                else
                {
                    Events.DoneRefreshCycle(this.periodicRefreshRate);
                    this.isInitialized.Set(true);
                    this.ServiceIdentitiesUpdated?.Invoke(this, await this.serviceIdentityHierarchy.GetAllIds());

                    lock (this.refreshCacheLock)
                    {
                        // Update the cache refresh timestamp
                        this.cacheLastRefreshTime = DateTime.UtcNow;

                        // Send the completion signal first, then reset the
                        // refresh signal to signify that we're no longer
                        // doing any work on this thread
                        this.refreshCacheCompleteSignal.Set();
                        this.refreshCacheSignal.Reset();
                    }

                    await this.IsReady(this.periodicRefreshRate);
                }
            }
        }

        bool ShouldRetryRefreshCache(bool lastRefreshSucceeded) => !lastRefreshSucceeded && !this.isInitialized.Get();

        async Task<bool> RefreshCacheInternal()
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
                IList<string> allIds = await this.serviceIdentityHierarchy.GetAllIds();
                IList<string> removedIds = allIds.Except(currentCacheIds).ToList();
                await Task.WhenAll(removedIds.Select(id => this.HandleNoServiceIdentity(id)));

                return true;
            }
            catch (Exception e)
            {
                Events.ErrorInRefreshCycle(e);
                return false;
            }
        }

        async Task IsReady(TimeSpan delay)
        {
            Task refreshCacheSignalTask = this.refreshCacheSignal.WaitAsync();
            Task sleepTask = Task.Delay(delay);
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
            return await this.serviceIdentityHierarchy.Get(id);
        }

        async Task HandleNoServiceIdentity(string id)
        {
            Option<ServiceIdentity> identity = await this.serviceIdentityHierarchy.Get(id);
            bool hasValidServiceIdentity = identity.Filter(s => s.Status == ServiceIdentityStatus.Enabled).HasValue;

            // Remove the target identity
            await this.serviceIdentityHierarchy.Remove(id);
            await this.store.Remove(id);
            Events.NotInScope(id);

            if (hasValidServiceIdentity)
            {
                // Remove device if connected, if service identity existed and then was removed.
                this.ServiceIdentityRemoved?.Invoke(this, id);
            }
        }

        async Task HandleNewServiceIdentity(ServiceIdentity serviceIdentity)
        {
            Option<ServiceIdentity> existing = await this.serviceIdentityHierarchy.Get(serviceIdentity.Id);

            bool hasChanged = await this.serviceIdentityHierarchy.AddOrUpdate(serviceIdentity);
            if (hasChanged)
            {
                Events.AddInScope(serviceIdentity.Id);
                await this.store.Save(serviceIdentity.Id, new StoredServiceIdentity(serviceIdentity));

                if (existing.HasValue)
                {
                    this.ServiceIdentityUpdated?.Invoke(this, serviceIdentity);
                }
            }
        }

        Task<Option<PurchaseContent>> GetPurchaseAsync(string id, Option<PurchaseContent> purchase, bool refresh = false)
        {
            return purchase.Match(p => Task.FromResult(Option.Some(p)), async () => {
                var lastSingleIdenityRefresh = identitiesLastRefreshTime.ContainsKey(id) ? identitiesLastRefreshTime[id] : DateTime.MinValue;
                if (refresh && lastSingleIdenityRefresh > cacheLastRefreshTime)
                {
                    InitiateCacheRefresh();
                    await WaitForCacheRefresh(TimeSpan.FromSeconds(60));
                    var updatedidentity = await this.GetServiceIdentityInternal(id);
                    
                    return await GetPurchaseAsync(id, updatedidentity.AndThen(i => i.PurchaseContent), false);
                }
                else
                {
                    return Option.None<PurchaseContent>();
                }
            });
        }

        public async Task<Option<PurchaseContent>> GetPurchaseAsync(string deviceId, string moduleId)
        {
            // TODO: format module id
            string id = deviceId + "/" + moduleId;

            var serviceIdentity = await this.GetServiceIdentityInternal(id);
            return await serviceIdentity.Match(
                si =>
                {
                    return GetPurchaseAsync(id, si.PurchaseContent, true);
                },
                () =>
                {
                    return Task.FromResult(Option.None<PurchaseContent>());
                });
        }

        internal class ServiceIdentityStore : IDisposable
        {
            static readonly string DeviceScopeFormat = "ms-azure-iot-edge://{0}-{1}";
            readonly IKeyValueStore<string, string> entityStore;

            public ServiceIdentityStore(IKeyValueStore<string, string> entityStore)
            {
                this.entityStore = entityStore;
            }

            public async Task Save(string id, StoredServiceIdentity storedServiceIdentity)
            {
                string serviceIdentityString = JsonConvert.SerializeObject(storedServiceIdentity);
                await this.entityStore.Put(id, serviceIdentityString);
            }

            public Task Remove(string id) => this.entityStore.Remove(id);

            public async Task<IDictionary<string, StoredServiceIdentity>> ReadCacheFromStore(IKeyValueStore<string, string> encryptedStore, string actorDeviceId)
            {
                IDictionary<string, StoredServiceIdentity> cache = new Dictionary<string, StoredServiceIdentity>();
                await encryptedStore.IterateBatch(
                    int.MaxValue,
                    (key, value) =>
                    {
                        var storedIdentity = JsonConvert.DeserializeObject<StoredServiceIdentity>(value);
                        cache.Add(key, storedIdentity);
                        return Task.CompletedTask;
                    });

                await this.RestoreDeviceScope(encryptedStore, actorDeviceId, cache);

                return cache;
            }

            public void Dispose() => this.entityStore?.Dispose();

            // Version 1.1 didn't have deviceScope in store, this method sets the deviceScope for edge device from deviceId and generationid
            // for leaf devices set the DeviceScope and ParentScopes to the edge device scope because if they are present in store they must be children of the edge device
            async Task RestoreDeviceScope(IKeyValueStore<string, string> encryptedStore, string actorDeviceId, IDictionary<string, StoredServiceIdentity> cache)
            {
                if (cache.TryGetValue(actorDeviceId, out StoredServiceIdentity storedServiceIdentity))
                {
                    string edgeDeviceScope = null;
                    storedServiceIdentity.ServiceIdentity.ForEach(si => edgeDeviceScope = si.DeviceScope.OrDefault());

                    if (string.IsNullOrEmpty(edgeDeviceScope) && storedServiceIdentity.ServiceIdentity.HasValue)
                    {
                        var edgeServiceIdentity = storedServiceIdentity.ServiceIdentity.OrDefault();
                        edgeDeviceScope = string.Format(DeviceScopeFormat, edgeServiceIdentity.DeviceId, edgeServiceIdentity.GenerationId);
                        List<string> keys = new List<string>(cache.Keys);
                        foreach (var key in keys)
                        {
                            if (key.Equals(actorDeviceId))
                            {
                                cache[key] = new StoredServiceIdentity(new ServiceIdentity(edgeServiceIdentity.DeviceId, edgeServiceIdentity.ModuleId.OrDefault(), edgeDeviceScope, edgeServiceIdentity.ParentScopes, edgeServiceIdentity.GenerationId, edgeServiceIdentity.Capabilities, edgeServiceIdentity.Authentication, edgeServiceIdentity.Status));
                            }
                            else
                            {
                                cache[key].ServiceIdentity.Filter(si => !si.IsEdgeDevice && !si.IsModule && !si.DeviceScope.HasValue && si.ParentScopes.Count == 0).ForEach(si =>
                                {
                                    cache[key] = new StoredServiceIdentity(new ServiceIdentity(si.DeviceId, si.ModuleId.OrDefault(), edgeDeviceScope, new List<string> { edgeDeviceScope }, si.GenerationId, si.Capabilities, si.Authentication, si.Status));
                                });
                            }

                            string serviceIdentityString = JsonConvert.SerializeObject(cache[key]);
                            await encryptedStore.Put(key, serviceIdentityString);
                        }
                    }
                }
            }
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
                SkipRefreshCache,
                RefreshSleepCompleted,
                RefreshSignalled,
                NotInScope,
                AddInScope,
                RefreshingServiceIdentity,
                SkipRefreshServiceIdentity,
                RefreshingAuthChain,
                GettingServiceIdentity,
                VerifyServiceIdentity,
                RetryCycle,
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

            public static void RetryRefreshCycle(TimeSpan refreshRate) =>
               Log.LogInformation((int)EventIds.RetryCycle, $"Retry refreshing device scope identities cache. Waiting for {refreshRate.TotalSeconds} seconds.");

            public static void DoneRefreshCycle(TimeSpan refreshRate) =>
                Log.LogInformation((int)EventIds.DoneCycle, $"Done refreshing device scope identities cache. Waiting for {refreshRate.TotalMinutes} minutes.");

            public static void ErrorRefreshingCache(Exception exception, string deviceId, string onBehalfOfDevice)
            {
                Log.LogWarning((int)EventIds.ErrorInRefresh, exception, $"Error while refreshing the service identity: {deviceId} OnBehalfOf: {onBehalfOfDevice}");
            }

            public static void ErrorProcessing(ServiceIdentity serviceIdentity, Exception exception)
            {
                string id = serviceIdentity?.Id ?? "unknown";
                Log.LogWarning((int)EventIds.ErrorInRefresh, exception, $"Error while processing the service identity for {id}");
            }

            public static void ReceivedRequestToRefreshCache() =>
                Log.LogDebug((int)EventIds.ReceivedRequestToRefreshCache, "Received request to refresh cache.");

            public static void SkipRefreshCache(DateTime lastRefreshTime, TimeSpan refreshDelay)
            {
                TimeSpan timeSinceLastRefresh = DateTime.UtcNow - lastRefreshTime;
                int secondsUntilNextRefresh = (int)(refreshDelay.TotalSeconds - timeSinceLastRefresh.TotalSeconds);
                Log.LogInformation((int)EventIds.SkipRefreshCache, $"Skipping cache refresh, waiting {secondsUntilNextRefresh} seconds until refreshing again.");
            }

            public static void RefreshSignalled() =>
                Log.LogInformation((int)EventIds.RefreshSignalled, "Device scope identities refresh is ready because a refresh was signalled.");

            public static void RefreshSleepCompleted() =>
                Log.LogDebug((int)EventIds.RefreshSleepCompleted, "Device scope identities refresh is ready because the wait period is over.");

            public static void NotInScope(string id) =>
                Log.LogDebug((int)EventIds.NotInScope, $"{id} is not in device scope, removing from cache.");

            public static void AddInScope(string id) =>
                Log.LogDebug((int)EventIds.AddInScope, $"{id} is in device scope, adding to cache.");

            public static void GettingServiceIdentity(string id) =>
                Log.LogDebug((int)EventIds.GettingServiceIdentity, $"Getting service identity for {id}");

            public static void RefreshingServiceIdentity(string id) =>
                Log.LogInformation((int)EventIds.RefreshingServiceIdentity, $"Refreshing service identity for {id}");

            public static void RefreshingAuthChain(string authChain) =>
                Log.LogDebug((int)EventIds.RefreshingAuthChain, $"Refreshing authChain {authChain}");

            public static void SkipRefreshServiceIdentity(string id, DateTime lastRefreshTime, TimeSpan refreshDelay)
            {
                TimeSpan timeSinceLastRefresh = DateTime.UtcNow - lastRefreshTime;
                int secondsUntilNextRefresh = (int)(refreshDelay.TotalSeconds - timeSinceLastRefresh.TotalSeconds);
                Log.LogInformation((int)EventIds.SkipRefreshServiceIdentity, $"Skipping refresh for identity: {id}, waiting {secondsUntilNextRefresh} seconds until refreshing again.");
            }

            public static void InitializingRefreshTask(TimeSpan refreshRate) =>
                Log.LogDebug((int)EventIds.InitializingRefreshTask, $"Initializing device scope identities cache refresh task to run every {refreshRate.TotalMinutes} minutes.");

            internal static void VerifyServiceIdentityFailure(string id, string reason) =>
                Log.LogDebug((int)EventIds.VerifyServiceIdentity, $"Service identity {id} is not valid because: {reason}.");
        }
    }
}
