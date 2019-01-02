// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    
    class CloudSync : ICloudSync
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
                ErrorSyncingReportedPropertiesToCloud,
                SendDesiredPropertyUpdates,
                StoringReportedPropertiesInStore,
                UpdatingReportedPropertiesInStore,
                SyncingReportedPropertiesToCloud,
                StoredReportedPropertiesFound,
                UpdateReportedPropertiesSucceeded,
                UpdateReportedPropertiesFailed,
                MergedReportedProperties,
                MergedDesiredProperties,
                UpdatingDesiredProperties,
                DoneUpdatingTwin,
                UpdatingTwin,
                GettingTwin,
                GettingTwinFromStore,
                GotTwinFromTwin,
                TwinSyncedRecently,
                GetTwinSucceeded,
                ErrorGettingTwin,
                UpdatingReportedProperties,
                UpdatedReportedProperties,
                ErrorUpdatingReportedProperties,
                ErrorHandlingDeviceConnected,
                HandlingDeviceConnectedCallback,
                NoTwinForDesiredPropertiesPatch
            }

            public static void ErrorUpdatingReportedProperties(string id, Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorUpdatingReportedProperties, ex, $"Error updating reported properties for {id}");
            }

            public static void UpdatedReportedProperties(string id)
            {
                Log.LogInformation((int)EventIds.UpdatedReportedProperties, $"Updated reported properties for {id}");
            }

            public static void UpdatingReportedProperties(string id)
            {
                Log.LogDebug((int)EventIds.UpdatingReportedProperties, $"Updating reported properties for {id}");
            }

            public static void ErrorGettingTwin(string id, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorGettingTwin, ex, $"Error getting twin for {id}");
            }

            public static void GetTwinSucceeded(string id)
            {
                Log.LogDebug((int)EventIds.GetTwinSucceeded, $"Got twin for {id}");
            }

            public static void GettingTwin(string id)
            {
                Log.LogDebug((int)EventIds.GettingTwin, $"Getting twin for {id}");
            }

            public static void DoneUpdatingTwin(string id)
            {
                Log.LogDebug((int)EventIds.DoneUpdatingTwin, $"Updated twin in store for {id}");
            }

            public static void UpdatingTwin(string id)
            {
                Log.LogDebug((int)EventIds.UpdatingTwin, $"Updating twin in store for {id}");
            }

            public static void MergedDesiredProperties(string id)
            {
                Log.LogDebug((int)EventIds.MergedDesiredProperties, $"Merged desired properties for {id} in store");
            }

            public static void UpdatingDesiredProperties(string id)
            {
                Log.LogDebug((int)EventIds.UpdatingDesiredProperties, $"Updating desired properties for {id}");
            }

            public static void MergedReportedProperties(string id)
            {
                Log.LogDebug((int)EventIds.MergedReportedProperties, $"Merged reported properties in store for {id}");
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

            public static void SendDesiredPropertyUpdates(string id)
            {
                Log.LogDebug((int)EventIds.SendDesiredPropertyUpdates, $"Sending desired property updates to {id}");
            }

            public static void ErrorSyncingReportedPropertiesToCloud(string id, Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorSyncingReportedPropertiesToCloud, ex, $"Error syncing reported properties to cloud for {id}");
            }

            public static void GettingTwinFromStore(string id)
            {
                Log.LogDebug((int)EventIds.GettingTwinFromStore, $"Getting twin for {id} from store");
            }

            public static void GotTwinFromCloud(string id)
            {
                Log.LogDebug((int)EventIds.GotTwinFromTwin, $"Got twin for {id} from cloud");
            }

            public static void TwinSyncedRecently(string id, DateTime syncTime, TimeSpan timeSpan)
            {
                Log.LogDebug((int)EventIds.TwinSyncedRecently, $"Twin for {id} synced at {syncTime} which is sooner than twin sync period {timeSpan.TotalSeconds} secs, skipping syncing twin");
            }

            public static void NoSyncTwinNotStored(string id)
            {
                Log.LogDebug((int)EventIds.NoSyncTwinNotStored, $"Not syncing twin on device connect for {id} as the twin does not exist in the store");
            }

            public static void UpdatingTwinOnDeviceConnect(string id)
            {
                Log.LogDebug((int)EventIds.UpdatingTwinOnDeviceConnect, $"Updated twin for {id} on device connect event");
            }

            public static void DoneSyncingReportedProperties(string id)
            {
                Log.LogInformation((int)EventIds.DoneSyncingReportedProperties, $"Done syncing reported properties for {id}");
            }

            internal static void ErrorSyncingReportedPropertiesToCloud(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorSyncingReportedPropertiesToCloud, e, $"Error in pump to sync reported properties to cloud");
            }

            public static void NoTwinForDesiredPropertiesPatch(string id)
            {
                Log.LogInformation((int)EventIds.NoTwinForDesiredPropertiesPatch, $"Cannot store desired properties patch  for {id} in store as twin was not found");
            }
        }
    }
}
