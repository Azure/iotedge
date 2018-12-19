// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Nito.AsyncEx;

    public class StoringTwinManager : ITwinManager
    {
        readonly IMessageConverter<TwinCollection> twinCollectionConverter;
        readonly IMessageConverter<Twin> twinConverter;
        readonly IConnectionManager connectionManager;
        readonly IValidator<TwinCollection> reportedPropertiesValidator;
        readonly ReportedPropertiesStore reportedPropertiesStore;
        readonly TwinStore twinEntityStore;
        readonly CloudSync cloudSync;
        readonly TimeSpan twinSyncPeriod;
        readonly ConcurrentDictionary<string, DateTime> twinSyncTime = new ConcurrentDictionary<string, DateTime>();

        StoringTwinManager(
            IConnectionManager connectionManager,
            IMessageConverter<TwinCollection> twinCollectionConverter,
            IMessageConverter<Twin> twinConverter,
            IValidator<TwinCollection> reportedPropertiesValidator,
            IEntityStore<string, TwinStoreEntity> twinStore,
            TimeSpan twinSyncPeriod)
        {
            Preconditions.CheckNotNull(twinStore, nameof(twinStore));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.twinCollectionConverter = Preconditions.CheckNotNull(twinCollectionConverter, nameof(twinCollectionConverter));
            this.twinConverter = Preconditions.CheckNotNull(twinConverter, nameof(twinConverter));
            this.cloudSync = new CloudSync(connectionManager, twinCollectionConverter, twinConverter);
            this.twinEntityStore = new TwinStore(twinStore);
            this.reportedPropertiesStore = new ReportedPropertiesStore(twinStore, this.cloudSync);
            this.reportedPropertiesValidator = reportedPropertiesValidator;
            this.twinSyncPeriod = twinSyncPeriod;
        }

        public static ITwinManager CreateTwinManager(
            IConnectionManager connectionManager,
            IMessageConverterProvider messageConverterProvider,
            IEntityStore<string, TwinStoreEntity> twinStore,
            IDeviceConnectivityManager deviceConnectivityManager,
            IValidator<TwinCollection> reportedPropertiesValidator,
            TimeSpan twinSyncPeriod)
        {
            Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            Preconditions.CheckNotNull(twinStore, nameof(twinStore));
            Preconditions.CheckNotNull(deviceConnectivityManager, nameof(deviceConnectivityManager));
            Preconditions.CheckNotNull(reportedPropertiesValidator, nameof(reportedPropertiesValidator));

            var twinManager = new StoringTwinManager(
                connectionManager,
                messageConverterProvider.Get<TwinCollection>(),
                messageConverterProvider.Get<Twin>(),
                reportedPropertiesValidator,
                twinStore,
                twinSyncPeriod);

            deviceConnectivityManager.DeviceConnected += (_, __) => twinManager.DeviceConnectedCallback();
            Events.StoringTwinManagerCreated();
            return twinManager;
        }

        async void DeviceConnectedCallback()
        {
            try
            {
                Events.HandlingDeviceConnectedCallback();
                IEnumerable<IIdentity> connectedClients = this.connectionManager.GetConnectedClients();
                foreach (IIdentity client in connectedClients)
                {
                    string id = client.Id;
                    try
                    {                        
                        await this.reportedPropertiesStore.SyncToCloud(id);
                        await this.SyncTwinAndSendDesiredPropertyUpdates(id);
                    }
                    catch (Exception e)
                    {
                        Events.ErrorHandlingDeviceConnected(e, id);
                    }
                }
            }
            catch (Exception ex)
            {
                Events.ErrorInDeviceConnectedCallback(ex);
            }
        }

        async Task SyncTwinAndSendDesiredPropertyUpdates(string id)
        {
            Option<Twin> storeTwin = await this.twinEntityStore.Get(id);
            if (!storeTwin.HasValue)
            {
                Events.NoSyncTwinNotStored(id);
            }
            else
            {
                await storeTwin.ForEachAsync(
                    async twin =>
                    {
                        if (!this.twinSyncTime.TryGetValue(id, out DateTime syncTime) ||
                            DateTime.UtcNow - syncTime > this.twinSyncPeriod)
                        {
                            Option<Twin> twinOption = await this.cloudSync.GetTwin(id);
                            await twinOption.ForEachAsync(
                                async cloudTwin =>
                                {
                                    Events.UpdatingTwinOnDeviceConnect(id);
                                    await this.twinEntityStore.Update(id, cloudTwin);
                                    this.twinSyncTime.AddOrUpdate(id, DateTime.UtcNow, (_, __) => DateTime.UtcNow);

                                    string diffPatch = JsonEx.Diff(twin.Properties.Desired, cloudTwin.Properties.Desired);
                                    if (string.IsNullOrWhiteSpace(diffPatch))
                                    {
                                        var patch = new TwinCollection(diffPatch);
                                        IMessage patchMessage = this.twinCollectionConverter.ToMessage(patch);
                                        await this.SendPatchToDevice(id, patchMessage);
                                    }
                                });
                        }
                        else
                        {
                            Events.TwinSyncedRecently(id, this.twinSyncPeriod);
                        }
                    });
            }
        }

        public async Task<IMessage> GetTwinAsync(string id)
        {
            Option<Twin> twinOption = await this.cloudSync.GetTwin(id);
            Twin twin = await twinOption.Map(
                    async t =>
                    {
                        Events.GotTwinFromTwin(id);
                        await this.twinEntityStore.Update(id, t);
                        this.twinSyncTime.AddOrUpdate(id, DateTime.UtcNow, (_, __) => DateTime.UtcNow);
                        return t;
                    })
                .GetOrElse(async () =>
                {
                    Events.GettingTwinFromStore(id);
                    Option<Twin> storedTwin = await this.twinEntityStore.Get(id);
                    return storedTwin.GetOrElse(() => new Twin());
                });
            return this.twinConverter.ToMessage(twin);
        }

        public async Task UpdateDesiredPropertiesAsync(string id, IMessage twinCollection)
        {
            Events.UpdatingDesiredProperties(id);
            TwinCollection patch = this.twinCollectionConverter.FromMessage(twinCollection);
            await this.twinEntityStore.UpdateDesiredProperties(id, patch);
            await this.SendPatchToDevice(id, twinCollection);
        }

        public async Task UpdateReportedPropertiesAsync(string id, IMessage twinCollection)
        {
            Events.UpdatingReportedProperties(id);
            TwinCollection patch = this.twinCollectionConverter.FromMessage(twinCollection);
            this.reportedPropertiesValidator.Validate(patch);
            await this.twinEntityStore.UpdateReportedProperties(id, patch);
            await this.reportedPropertiesStore.Update(id, patch);
            try
            {
                await this.reportedPropertiesStore.SyncToCloud(id);
            }
            catch (Exception ex)
            {
                Events.ErrorSyncingReportedPropertiesToCloud(id, ex);
            }
        }

        Task SendPatchToDevice(string id, IMessage twinCollection)
        {
            Events.SendDesiredPropertyUpdates(id);
            Option<IReadOnlyDictionary<DeviceSubscription, bool>> subscriptionsOption = this.connectionManager.GetSubscriptions(id);
            return subscriptionsOption.ForEachAsync(
                subscriptions =>
                {
                    if (subscriptions.TryGetValue(DeviceSubscription.DesiredPropertyUpdates, out bool desiredPropertyUpdates) && desiredPropertyUpdates)
                    {
                        Events.SendDesiredPropertyUpdates(id);
                        Option<IDeviceProxy> deviceProxyOption = this.connectionManager.GetDeviceConnection(id);
                        return deviceProxyOption.ForEachAsync(deviceProxy => deviceProxy.OnDesiredPropertyUpdates(twinCollection));
                    }
                    else
                    {
                        Events.SendDesiredPropertyUpdates(id);
                    }

                    return Task.CompletedTask;
                });
        }

        internal class ReportedPropertiesStore
        {
            readonly IEntityStore<string, TwinStoreEntity> twinStore;
            readonly CloudSync cloudSync;
            readonly AsyncLockProvider<string> lockProvider = new AsyncLockProvider<string>(10);
            readonly AsyncAutoResetEvent syncToCloudSignal = new AsyncAutoResetEvent(false);
            readonly ConcurrentQueue<string> syncToCloudQueue = new ConcurrentQueue<string>();
            readonly Task syncToCloudTask;

            public ReportedPropertiesStore(IEntityStore<string, TwinStoreEntity> twinStore, CloudSync cloudSync)
            {
                this.twinStore = twinStore;
                this.cloudSync = cloudSync;
                this.syncToCloudTask = this.SyncToCloud();
            }

            public async Task Update(string id, TwinCollection patch)
            {
                using (await this.lockProvider.GetLock(id).LockAsync())
                {
                    Events.StoringReportedPropertiesInStore(id, patch);
                    await this.twinStore.PutOrUpdate(
                        id,
                        new TwinStoreEntity(patch),
                        twinInfo =>
                        {
                            Events.UpdatingReportedPropertiesInStore(id, patch);
                            TwinCollection updatedReportedProperties = twinInfo.ReportedPropertiesPatch
                                .Map(reportedProperties => new TwinCollection(JsonEx.Merge(reportedProperties, patch, /*treatNullAsDelete*/ false)))
                                .GetOrElse(() => patch);
                            return new TwinStoreEntity(twinInfo.Twin, Option.Maybe(updatedReportedProperties));
                        });
                }
            }

            public void InitSyncToCloud(string id)
            {
                this.syncToCloudQueue.Enqueue(id);
                this.syncToCloudSignal.Set();
            }

            async Task SyncToCloud()
            {
                while (true)
                {
                    try
                    {
                        await this.syncToCloudSignal.WaitAsync();
                        while (this.syncToCloudQueue.TryDequeue(out string id))
                        {
                            await this.SyncToCloud(id);
                        }                        
                    }
                    catch (Exception e)
                    {
                        Events.ErrorSyncingReportedPropertiesToCloud(e);
                    }
                }
            }

            public async Task SyncToCloud(string id)
            {
                Events.SyncingReportedPropertiesToCloud(id);
                Option<TwinStoreEntity> twinWithReportedProperties = (await this.twinStore.Get(id))
                    .Filter(t => t.ReportedPropertiesPatch.HasValue);
                if (twinWithReportedProperties.HasValue)
                {                            
                    using (await this.lockProvider.GetLock(id).LockAsync())
                    {
                        Events.StoredReportedPropertiesFound(id);
                        Option<TwinStoreEntity> twinInfo = await this.twinStore.Get(id);
                        await twinInfo.ForEachAsync(
                            async ti =>
                            {
                                await ti.ReportedPropertiesPatch.ForEachAsync(
                                    async reportedPropertiesPatch =>
                                    {
                                        bool result = await this.cloudSync.UpdateReportedProperties(id, reportedPropertiesPatch);

                                        if (result)
                                        {
                                            Events.UpdateReportedPropertiesSucceeded(id);
                                            await this.twinStore.Update(
                                                id,
                                                t => new TwinStoreEntity(t.Twin, Option.None<TwinCollection>()));
                                        }
                                        else
                                        {
                                            Events.UpdateReportedPropertiesFailed(id);
                                        }
                                    });
                            });
                    }
                }
                else
                {
                    Events.DoneSyncingReportedProperties(id);
                }
            }
        }

        internal class TwinStore
        {
            readonly IEntityStore<string, TwinStoreEntity> twinStore;

            public TwinStore(IEntityStore<string, TwinStoreEntity> twinStore)
            {
                this.twinStore = twinStore;
            }

            public async Task<Option<Twin>> Get(string id)
            {
                Option<TwinStoreEntity> twinStoreEntity = await this.twinStore.Get(id);
                return twinStoreEntity.FlatMap(t => t.Twin);
            }

            public async Task UpdateReportedProperties(string id, TwinCollection patch)
            {
                Events.UpdatingReportedProperties(id);
                await this.twinStore.Update(
                    id,
                    twinInfo =>
                    {
                        twinInfo.Twin
                            .ForEach(
                                twin =>
                                {
                                    TwinProperties twinProperties = twin.Properties ?? new TwinProperties();
                                    TwinCollection reportedProperties = twinProperties.Reported ?? new TwinCollection();
                                    string mergedReportedPropertiesString = JsonEx.Merge(reportedProperties, patch, /* treatNullAsDelete */ true);
                                    twinProperties.Reported = new TwinCollection(mergedReportedPropertiesString);
                                    twin.Properties = twinProperties;
                                    Events.MergedReportedProperties(id);
                                });
                        return twinInfo;
                    });
            }

            public async Task UpdateDesiredProperties(string id, TwinCollection patch)
            {
                Events.UpdatingDesiredProperties(id);
                await this.twinStore.Update(
                    id,
                    twinInfo =>
                    {
                        twinInfo.Twin
                            .ForEach(
                                twin =>
                                {
                                    TwinProperties twinProperties = twin.Properties ?? new TwinProperties();
                                    TwinCollection desiredProperties = twinProperties.Desired ?? new TwinCollection();
                                    if (desiredProperties.Version + 1 == patch.Version)
                                    {
                                        string mergedDesiredPropertiesString = JsonEx.Merge(desiredProperties, patch, /* treatNullAsDelete */ true);
                                        twinProperties.Desired = new TwinCollection(mergedDesiredPropertiesString);
                                        twin.Properties = twinProperties;
                                        Events.MergedDesiredProperties(id);
                                    }
                                });


                        return twinInfo;
                    });
            }

            public async Task Update(string id, Twin twin)
            {
                Events.UpdatingTwin(id);
                await this.twinStore.PutOrUpdate(
                    id,
                    new TwinStoreEntity(twin),
                    twinInfo =>
                    {
                        return twinInfo.Twin
                            .Filter(
                                storedTwin => storedTwin.Properties?.Desired?.Version < twin.Properties.Desired.Version
                                    && storedTwin.Properties?.Reported?.Version < twin.Properties.Reported.Version)
                            .Map(_ => new TwinStoreEntity(Option.Maybe(twin), twinInfo.ReportedPropertiesPatch))
                            .GetOrElse(twinInfo);
                    });
                Events.DoneUpdatingTwin(id);
            }
        }

        internal class CloudSync
        {
            readonly IConnectionManager connectionManager;
            readonly IMessageConverter<TwinCollection> twinCollectionConverter;
            readonly IMessageConverter<Twin> twinConverter;

            public CloudSync(
                IConnectionManager connectionManager,
                IMessageConverter<TwinCollection> twinCollectionConverter,
                IMessageConverter<Twin> twinConverter)
            {
                this.connectionManager = connectionManager;
                this.twinCollectionConverter = twinCollectionConverter;
                this.twinConverter = twinConverter;
            }

            public async Task<Option<Twin>> GetTwin(string id)
            {
                try
                {
                    Events.GettingTwin(id);
                    Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                    Option<Twin> twin = await cloudProxy.Map(
                            async cp =>
                            {
                                IMessage twinMessage = await cp.GetTwinAsync();
                                Twin twinValue = this.twinConverter.FromMessage(twinMessage);
                                Events.GetTwinSucceeded(id);
                                return Option.Some(twinValue);
                            })
                        .GetOrElse(() => Task.FromResult(Option.None<Twin>()));
                    return twin;
                }
                catch (Exception ex)
                {
                    Events.ErrorGettingTwin(id, ex);
                    return Option.None<Twin>();
                }
            }

            public async Task<bool> UpdateReportedProperties(string id, TwinCollection patch)
            {
                try
                {
                    Events.UpdatingReportedProperties(id);
                    Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                    bool result = await cloudProxy.Map(
                            async cp =>
                            {
                                IMessage patchMessage = this.twinCollectionConverter.ToMessage(patch);
                                await cp.UpdateReportedPropertiesAsync(patchMessage);
                                Events.UpdatedReportedProperties(id);
                                return true;
                            })
                        .GetOrElse(() => Task.FromResult(false));
                    return result;
                }
                catch (Exception ex)
                {
                    Events.ErrorUpdatingReportedProperties(id, ex);
                    return false;
                }
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<StoringTwinManager>();
            const int IdStart = HubCoreEventIds.TwinManager;

            enum EventIds
            {
                ErrorInDeviceConnectedCallback = IdStart,
                StoringTwinManagerCreated,
                DoneSyncingReportedProperties,
                NoSyncTwinNotStored,
                UpdatingTwinOnDeviceConnect,
                ErrorSyncingReportedPropertiesToCloud
            }

            public static void ErrorInDeviceConnectedCallback(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorInDeviceConnectedCallback, ex, "Error in device connected callback");
            }

            public static void StoringTwinManagerCreated()
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Storing twin manager created");
            }

            public static void HandlingDeviceConnectedCallback()
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Received device connected callback");
            }

            public static void ErrorHandlingDeviceConnected(Exception ex, string id)
            {
                Log.LogWarning((int)EventIds.StoringTwinManagerCreated, ex, $"Error handling device connected event for {id}");
            }

            public static void ErrorUpdatingReportedProperties(string id, Exception ex)
            {
                Log.LogDebug((int)EventIds.StoringTwinManagerCreated, ex, $"Error updating reported properties for {id}");
            }
            
            public static void UpdatedReportedProperties(string id)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Updated reported properties for {id}");
            }

            public static void UpdatingReportedProperties(string id)
            {
                Log.LogDebug((int)EventIds.StoringTwinManagerCreated, $"Updating reported properties for {id}");
            }

            public static void ErrorGettingTwin(string id, Exception ex)
            {
                Log.LogWarning((int)EventIds.StoringTwinManagerCreated, ex, $"Error getting twin for {id}");
            }

            public static void GetTwinSucceeded(string id)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Got twin for {id}");
            }

            public static void GettingTwin(string id)
            {
                Log.LogDebug((int)EventIds.StoringTwinManagerCreated, $"Getting twin for {id}");
            }

            public static void DoneUpdatingTwin(string id)
            {
                Log.LogDebug((int)EventIds.StoringTwinManagerCreated, $"Updated twin in store for {id}");
            }

            public static void UpdatingTwin(string id)
            {
                Log.LogDebug((int)EventIds.StoringTwinManagerCreated, $"Updating twin in store for {id}");
            }

            public static void MergedDesiredProperties(string id)
            {
                Log.LogDebug((int)EventIds.StoringTwinManagerCreated, $"Merged desired properties in store");
            }

            public static void UpdatingDesiredProperties(string id)
            {
                Log.LogDebug((int)EventIds.StoringTwinManagerCreated, $"Updating desired properties for {id}");
            }

            public static void MergedReportedProperties(string id)
            {
                Log.LogDebug((int)EventIds.StoringTwinManagerCreated, $"Merged reported properties in store for {id}");
            }

            public static void UpdateReportedPropertiesFailed(string id)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Updating reported properties failed {id}");
            }

            public static void UpdateReportedPropertiesSucceeded(string id)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Updated reported properties for {id}");
            }

            public static void StoredReportedPropertiesFound(string id)
            {
                Log.LogDebug((int)EventIds.StoringTwinManagerCreated, $"Found stored reported properties to sync to cloud");
            }

            public static void SyncingReportedPropertiesToCloud(string id)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Syncing stored reported properties to cloud in {id}");
            }

            public static void UpdatingReportedPropertiesInStore(string id, TwinCollection patch)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Updating reported properties in store with version {patch.Version} for {id}");
            }

            public static void StoringReportedPropertiesInStore(string id, TwinCollection patch)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Storing reported properties in store for {id} with version {patch.Version}");
            }

            public static void SendDesiredPropertyUpdates(string id)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Sending desired property updates to {id}");
            }

            public static void ErrorSyncingReportedPropertiesToCloud(string id, Exception ex)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, ex, $"Error syncing reported properties to cloud for {id}");
            }

            public static void GettingTwinFromStore(string id)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Storing twin manager created");
            }

            public static void GotTwinFromTwin(string id)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Storing twin manager created");
            }

            public static void TwinSyncedRecently(string id, TimeSpan timeSpan)
            {
                Log.LogInformation((int)EventIds.StoringTwinManagerCreated, $"Storing twin manager created");
            }

            public static void NoSyncTwinNotStored(string id)
            {
                Log.LogDebug((int)EventIds.NoSyncTwinNotStored, $"Not syncing twin on device connect for {id} as the twin does not exist in the store");
            }

            public static void UpdatingTwinOnDeviceConnect(string id)
            {
                Log.LogInformation((int)EventIds.UpdatingTwinOnDeviceConnect, $"Updated twin for {id} on device connect event");
            }

            public static void DoneSyncingReportedProperties(string id)
            {
                Log.LogInformation((int)EventIds.DoneSyncingReportedProperties, $"Done syncing reported properties for {id}");
            }

            internal static void ErrorSyncingReportedPropertiesToCloud(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorSyncingReportedPropertiesToCloud, e, $"Error in pump to sync reported properties to cloud");
            }
        }
    }
}
