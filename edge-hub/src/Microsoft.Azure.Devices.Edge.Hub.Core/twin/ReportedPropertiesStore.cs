// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Nito.AsyncEx;

    class ReportedPropertiesStore : IReportedPropertiesStore
    {
        static readonly TimeSpan SleepTime = TimeSpan.FromSeconds(5);
        readonly IEntityStore<string, TwinStoreEntity> twinStore;
        readonly ICloudSync cloudSync;
        readonly AsyncLockProvider<string> lockProvider = new AsyncLockProvider<string>(10);
        readonly AsyncAutoResetEvent syncToCloudSignal = new AsyncAutoResetEvent(false);
        readonly ConcurrentQueue<string> syncToCloudQueue = new ConcurrentQueue<string>();
        readonly Task syncToCloudTask;

        public ReportedPropertiesStore(IEntityStore<string, TwinStoreEntity> twinStore, ICloudSync cloudSync)
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

                await Task.Delay(SleepTime);
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

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<StoringTwinManager>();
            const int IdStart = HubCoreEventIds.TwinManager;

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
