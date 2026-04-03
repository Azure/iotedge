// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class TwinStore : ITwinStore
    {
        readonly IEntityStore<string, TwinStoreEntity> twinEntityStore;

        public TwinStore(IEntityStore<string, TwinStoreEntity> twinEntityStore)
        {
            this.twinEntityStore = twinEntityStore;
        }

        public async Task<Option<TwinProperties>> Get(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Option<TwinStoreEntity> twinStoreEntity = await this.twinEntityStore.Get(id);
            return twinStoreEntity.FlatMap(t => t.Twin);
        }

        public async Task UpdateReportedProperties(string id, PropertyCollection patch)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Preconditions.CheckNotNull(patch, nameof(patch));
            Events.UpdatingReportedProperties(id);
            await this.twinEntityStore.PutOrUpdate(
                id,
                new TwinStoreEntity(new TwinProperties { Reported = patch }),
                twinInfo =>
                {
                    twinInfo.Twin
                        .ForEach(
                            twin =>
                            {
                                PropertyCollection reportedProperties = twin.Reported ?? new PropertyCollection();
                                string mergedReportedPropertiesString = JsonEx.Merge(reportedProperties, patch, /* treatNullAsDelete */ true);
                                twin.Reported = JsonConvert.DeserializeObject<PropertyCollection>(mergedReportedPropertiesString);
                                Events.MergedReportedProperties(id);
                            });
                    return twinInfo;
                });
        }

        public async Task UpdateDesiredProperties(string id, PropertyCollection patch)
        {
            Events.UpdatingDesiredProperties(id);
            Preconditions.CheckNotNull(patch, nameof(patch));
            Option<TwinProperties> storedTwin = await this.Get(id);
            if (storedTwin.HasValue)
            {
                await this.twinEntityStore.Update(
                    id,
                    twinInfo =>
                    {
                        twinInfo.Twin
                            .ForEach(
                                twin =>
                                {
                                    PropertyCollection desiredProperties = twin.Desired ?? new PropertyCollection();
                                    if (desiredProperties.Version + 1 == patch.Version)
                                    {
                                        string mergedDesiredPropertiesString = JsonEx.Merge(desiredProperties, patch, /* treatNullAsDelete */ true);
                                        twin.Desired = JsonConvert.DeserializeObject<PropertyCollection>(mergedDesiredPropertiesString);
                                        Events.MergedDesiredProperties(id);
                                    }
                                    else
                                    {
                                        Events.DesiredPropertiesVersionMismatch(id, desiredProperties.Version, patch.Version);
                                    }
                                });

                        return twinInfo;
                    });
            }
            else
            {
                Events.NoTwinForDesiredPropertiesPatch(id);
            }
        }

        public async Task Update(string id, TwinProperties twin)
        {
            Events.UpdatingTwin(id);
            Preconditions.CheckNotNull(twin, nameof(twin));
            // Don't check for version number here - just update the twin in the store.
            // This is because, if the module, say A, was deleted and recreated, then the
            // version number check won't really apply. So just override the twin.
            await this.twinEntityStore.PutOrUpdate(
                id,
                new TwinStoreEntity(twin),
                twinInfo => new TwinStoreEntity(Option.Some(twin), twinInfo.ReportedPropertiesPatch));
            Events.DoneUpdatingTwin(id);
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.TwinManager;
            static readonly ILogger Log = Logger.Factory.CreateLogger<StoringTwinManager>();

            enum EventIds
            {
                MergedReportedProperties = IdStart + 50,
                MergedDesiredProperties,
                UpdatingDesiredProperties,
                DoneUpdatingTwin,
                UpdatingTwin,
                UpdatingReportedProperties,
                NoTwinForDesiredPropertiesPatch,
                DesiredPropertiesVersionMismatch
            }

            public static void UpdatingReportedProperties(string id)
            {
                Log.LogDebug((int)EventIds.UpdatingReportedProperties, $"Updating reported properties for {id}");
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

            public static void NoTwinForDesiredPropertiesPatch(string id)
            {
                Log.LogInformation((int)EventIds.NoTwinForDesiredPropertiesPatch, $"Cannot store desired properties patch  for {id} in store as twin was not found");
            }

            public static void DesiredPropertiesVersionMismatch(string id, long desiredPropertiesVersion, long patchVersion)
            {
                Log.LogInformation((int)EventIds.DesiredPropertiesVersionMismatch, $"Skipped updating the desired properties for {id} because patch version {patchVersion} cannot be applied on current version {desiredPropertiesVersion}");
            }
        }
    }
}
