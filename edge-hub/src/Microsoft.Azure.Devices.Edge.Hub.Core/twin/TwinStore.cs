// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Twin
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    class TwinStore : ITwinStore
    {
        readonly IEntityStore<string, TwinStoreEntity> twinEntityStore;

        public TwinStore(IEntityStore<string, TwinStoreEntity> twinEntityStore)
        {
            this.twinEntityStore = twinEntityStore;
        }

        public async Task<Option<Twin>> Get(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Option<TwinStoreEntity> twinStoreEntity = await this.twinEntityStore.Get(id);
            return twinStoreEntity.FlatMap(t => t.Twin);
        }

        public async Task UpdateReportedProperties(string id, TwinCollection patch)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Preconditions.CheckNotNull(patch, nameof(patch));
            Events.UpdatingReportedProperties(id);
            await this.twinEntityStore.PutOrUpdate(
                id,
                new TwinStoreEntity(new Twin { Properties = new TwinProperties { Reported = patch } }),
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
            Preconditions.CheckNotNull(patch, nameof(patch));
            Option<Twin> storedTwin = await this.Get(id);
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
                                    TwinProperties twinProperties = twin.Properties ?? new TwinProperties();
                                    TwinCollection desiredProperties = twinProperties.Desired ?? new TwinCollection();
                                    if (desiredProperties.Version + 1 == patch.Version)
                                    {
                                        string mergedDesiredPropertiesString = JsonEx.Merge(desiredProperties, patch, /* treatNullAsDelete */ true);
                                        twinProperties.Desired = new TwinCollection(mergedDesiredPropertiesString);
                                        twin.Properties = twinProperties;
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

        public async Task Update(string id, Twin twin)
        {
            Events.UpdatingTwin(id);
            Preconditions.CheckNotNull(twin, nameof(twin));
            await this.twinEntityStore.PutOrUpdate(
                id,
                new TwinStoreEntity(twin),
                twinInfo =>
                {
                    return twinInfo.Twin
                        .Filter(
                            storedTwin => storedTwin.Properties?.Desired?.Version < twin.Properties.Desired.Version
                                          && storedTwin.Properties?.Reported?.Version < twin.Properties.Reported.Version)
                        .Map(_ => new TwinStoreEntity(Option.Some(twin), twinInfo.ReportedPropertiesPatch))
                        .GetOrElse(twinInfo);
                });
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
