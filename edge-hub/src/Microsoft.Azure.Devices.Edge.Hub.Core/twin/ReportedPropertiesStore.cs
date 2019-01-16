// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Nito.AsyncEx;

    class ReportedPropertiesStore : IReportedPropertiesStore
    {
        static readonly TimeSpan DefaultSyncFrequency = TimeSpan.FromSeconds(5);
        readonly IEntityStore<string, TwinStoreEntity> twinStore;
        readonly ICloudSync cloudSync;
        readonly AsyncLockProvider<string> lockProvider = new AsyncLockProvider<string>(10);
        readonly AsyncAutoResetEvent syncToCloudSignal = new AsyncAutoResetEvent(false);
        readonly HashSet<string> syncToCloudClients = new HashSet<string>(StringComparer.Ordinal);
        readonly object syncToCloudSetLock = new object();
        readonly Task syncToCloudTask;
        readonly TimeSpan syncFrequency;

        public ReportedPropertiesStore(IEntityStore<string, TwinStoreEntity> twinStore, ICloudSync cloudSync, Option<TimeSpan> syncFrequency)
        {
            this.twinStore = twinStore;
            this.cloudSync = cloudSync;
            this.syncToCloudTask = this.SyncToCloud();
            this.syncFrequency = syncFrequency.GetOrElse(DefaultSyncFrequency);
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
            lock (this.syncToCloudSetLock)
            {
                this.syncToCloudClients.Add(id);
            }

            this.syncToCloudSignal.Set();
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
                                        twinInfo = await this.twinStore.Get(id);
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

        async Task SyncToCloud()
        {
            while (true)
            {
                try
                {
                    // Take a snapshot of clients to process
                    IEnumerable<string> clientsToProcess;
                    lock (this.syncToCloudSetLock)
                    {
                        clientsToProcess = this.syncToCloudClients.ToList();
                    }

                    foreach (string id in clientsToProcess)
                    {
                        await this.SyncToCloud(id);
                    }
                }
                catch (Exception e)
                {
                    Events.ErrorSyncingReportedPropertiesToCloud(e);
                }

                // Wait for syncfrequency to avoid looping too fast,
                // then wait for the signal indicating more work is ready
                await Task.Delay(this.syncFrequency);
                await this.syncToCloudSignal.WaitAsync();
            }
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.TwinManager;
            static readonly ILogger Log = Logger.Factory.CreateLogger<StoringTwinManager>();

            enum EventIds
            {
                DoneSyncingReportedProperties = IdStart + 30,
                ErrorSyncingReportedPropertiesToCloud,
                StoringReportedPropertiesInStore,
                UpdatingReportedPropertiesInStore,
                SyncingReportedPropertiesToCloud,
                StoredReportedPropertiesFound,
                UpdateReportedPropertiesSucceeded,
                UpdateReportedPropertiesFailed
            }

            public static void UpdateReportedPropertiesFailed(string id)
            {
                Log.LogWarning((int)EventIds.UpdateReportedPropertiesFailed, $"Updating reported properties failed {id}");
            }

            public static void UpdateReportedPropertiesSucceeded(string id)
            {
                Log.LogDebug((int)EventIds.UpdateReportedPropertiesSucceeded, $"Updated reported properties for {id}");
            }

            public static void StoredReportedPropertiesFound(string id)
            {
                Log.LogDebug((int)EventIds.StoredReportedPropertiesFound, $"Found stored reported properties for {id} to sync to cloud");
            }

            public static void SyncingReportedPropertiesToCloud(string id)
            {
                Log.LogDebug((int)EventIds.SyncingReportedPropertiesToCloud, $"Syncing stored reported properties to cloud in {id}");
            }

            public static void UpdatingReportedPropertiesInStore(string id, TwinCollection patch)
            {
                Log.LogDebug((int)EventIds.UpdatingReportedPropertiesInStore, $"Updating reported properties in store with version {patch.Version} for {id}");
            }

            public static void StoringReportedPropertiesInStore(string id, TwinCollection patch)
            {
                Log.LogDebug((int)EventIds.StoringReportedPropertiesInStore, $"Storing reported properties in store for {id} with version {patch.Version}");
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
