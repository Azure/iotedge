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

    public class StoringTwinManager : ITwinManager
    {
        readonly IMessageConverter<TwinCollection> twinCollectionConverter;
        readonly IMessageConverter<Twin> twinConverter;
        readonly IConnectionManager connectionManager;
        readonly IValidator<TwinCollection> reportedPropertiesValidator;
        readonly ReportedPropertiesStore reportedPropertiesStore;
        readonly TwinStore twinEntityStore;
        readonly CloudSync cloudSync;
        readonly ConcurrentDictionary<string, DateTime> twinSyncTime = new ConcurrentDictionary<string, DateTime>();

        StoringTwinManager(
            IConnectionManager connectionManager,
            IMessageConverter<TwinCollection> twinCollectionConverter,
            IMessageConverter<Twin> twinConverter,
            IValidator<TwinCollection> reportedPropertiesValidator,
            IEntityStore<string, TwinStoreEntity> twinStore)
        {
            Preconditions.CheckNotNull(twinStore, nameof(twinStore));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.twinCollectionConverter = Preconditions.CheckNotNull(twinCollectionConverter, nameof(twinCollectionConverter));
            this.twinConverter = Preconditions.CheckNotNull(twinConverter, nameof(twinConverter));
            this.cloudSync = new CloudSync(connectionManager, twinCollectionConverter, twinConverter);
            this.twinEntityStore = new TwinStore(twinStore);
            this.reportedPropertiesStore = new ReportedPropertiesStore(twinStore, this.cloudSync);
            this.reportedPropertiesValidator = reportedPropertiesValidator;
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
                twinStore);
            deviceConnectivityManager.DeviceConnected += (_, __) => twinManager.DeviceConnectedCallback(twinSyncPeriod);
            return twinManager;
        }

        async void DeviceConnectedCallback(TimeSpan twinSyncPeriod)
        {
            try
            {
                IEnumerable<IIdentity> connectedClients = this.connectionManager.GetConnectedClients();
                foreach (IIdentity client in connectedClients)
                {
                    string id = client.Id;
                    await this.reportedPropertiesStore.SyncToCloud(id);
                    await this.GetTwinAndSendDesiredPropertyUpdates(id, twinSyncPeriod);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        async Task GetTwinAndSendDesiredPropertyUpdates(string id, TimeSpan twinSyncPeriod)
        {
            if (!this.twinSyncTime.TryGetValue(id, out DateTime syncTime) ||
                DateTime.UtcNow - syncTime > twinSyncPeriod)
            {
                Option<Twin> twinOption = await this.cloudSync.GetTwin(id);
                await twinOption.ForEachAsync(
                    async twin =>
                    {
                        Twin storeTwin = await this.twinEntityStore.Get(id);
                        string diffPatch = JsonEx.Diff(storeTwin.Properties.Desired, twin.Properties.Desired);
                        if (string.IsNullOrWhiteSpace(diffPatch))
                        {
                            var patch = new TwinCollection(diffPatch);
                            IMessage patchMessage = this.twinCollectionConverter.ToMessage(patch);
                            await this.SendPatchToDevice(id, patchMessage);
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
                        await this.twinEntityStore.Update(id, t);
                        this.twinSyncTime.AddOrUpdate(id, DateTime.UtcNow, (_, __) => DateTime.UtcNow);
                        return t;
                    })
                .GetOrElse(this.twinEntityStore.Get(id));
            return this.twinConverter.ToMessage(twin);
        }

        public async Task UpdateDesiredPropertiesAsync(string id, IMessage twinCollection)
        {
            TwinCollection patch = this.twinCollectionConverter.FromMessage(twinCollection);
            await this.twinEntityStore.UpdateDesiredProperties(id, patch);
            await this.SendPatchToDevice(id, twinCollection);
        }

        public async Task UpdateReportedPropertiesAsync(string id, IMessage twinCollection)
        {
            TwinCollection patch = this.twinCollectionConverter.FromMessage(twinCollection);
            this.reportedPropertiesValidator.Validate(patch);
            await this.twinEntityStore.UpdateReportedProperties(id, patch);
            await this.reportedPropertiesStore.Update(id, patch);
            try
            {
                await this.reportedPropertiesStore.SyncToCloud(id);
            }
            catch (Exception)
            {
                // Log
            }
        }

        Task SendPatchToDevice(string id, IMessage twinCollection)
        {
            Option<IReadOnlyDictionary<DeviceSubscription, bool>> subscriptionsOption = this.connectionManager.GetSubscriptions(id);
            return subscriptionsOption.ForEachAsync(
                subscriptions =>
                {
                    if (subscriptions.TryGetValue(DeviceSubscription.DesiredPropertyUpdates, out bool desiredPropertyUpdates) && desiredPropertyUpdates)
                    {
                        Option<IDeviceProxy> deviceProxyOption = this.connectionManager.GetDeviceConnection(id);
                        return deviceProxyOption.ForEachAsync(deviceProxy => deviceProxy.OnDesiredPropertyUpdates(twinCollection));
                    }

                    return Task.CompletedTask;
                });
        }

        internal class ReportedPropertiesStore
        {
            readonly IEntityStore<string, TwinStoreEntity> twinStore;
            readonly CloudSync cloudSync;
            readonly AsyncLockProvider<string> lockProvider = new AsyncLockProvider<string>(10);

            public ReportedPropertiesStore(IEntityStore<string, TwinStoreEntity> twinStore, CloudSync cloudSync)
            {
                this.twinStore = twinStore;
                this.cloudSync = cloudSync;
            }

            public async Task Update(string id, TwinCollection patch)
            {
                using (await this.lockProvider.GetLock(id).LockAsync())
                {
                    await this.twinStore.PutOrUpdate(
                        id,
                        new TwinStoreEntity(Option.Some(patch)),
                        twinInfo =>
                        {
                            TwinCollection updatedReportedProperties = twinInfo.ReportedPropertiesPatch
                                .Map(reportedProperties => new TwinCollection(JsonEx.Merge(reportedProperties, patch, /*treatNullAsDelete*/ false)))
                                .GetOrElse(() => patch);
                            return new TwinStoreEntity(twinInfo.Twin, updatedReportedProperties);
                        });
                }
            }

            public async Task SyncToCloud(string id)
            {
                Option<TwinStoreEntity> twinInfoOption = await this.twinStore.Get(id);
                await twinInfoOption.ForEachAsync(
                    async twinInfo =>
                    {
                        if (twinInfo.ReportedPropertiesPatch.HasValue)
                        {
                            using (await this.lockProvider.GetLock(id).LockAsync())
                            {
                                twinInfo = await this.twinStore.FindOrPut(id, new TwinStoreEntity());
                                await twinInfo.ReportedPropertiesPatch.ForEachAsync(
                                    async reportedPropertiesPatch =>
                                    {
                                        bool result = await this.cloudSync.UpdateReportedProperties(id, reportedPropertiesPatch);

                                        if (result)
                                        {
                                            await this.twinStore.Update(
                                                id,
                                                ti => new TwinStoreEntity(ti.Twin));
                                        }
                                    });
                            }
                        }
                    });
            }
        }

        internal class TwinStore
        {
            readonly IEntityStore<string, TwinStoreEntity> twinStore;

            public TwinStore(IEntityStore<string, TwinStoreEntity> twinStore)
            {
                this.twinStore = twinStore;
            }

            public async Task<Twin> Get(string id)
            {
                TwinStoreEntity twinInfo = await this.twinStore.FindOrPut(id, new TwinStoreEntity());
                return twinInfo.Twin;
            }

            public async Task UpdateReportedProperties(string id, TwinCollection patch)
            {
                await this.twinStore.PutOrUpdate(
                    id,
                    new TwinStoreEntity(new Twin(new TwinProperties { Reported = patch })),
                    twinInfo =>
                    {
                        TwinProperties twinProperties = twinInfo.Twin.Properties ?? new TwinProperties();
                        TwinCollection reportedProperties = twinProperties.Reported ?? new TwinCollection();
                        string mergedReportedPropertiesString = JsonEx.Merge(reportedProperties, patch, /* treatNullAsDelete */ true);
                        twinProperties.Reported = new TwinCollection(mergedReportedPropertiesString);
                        twinInfo.Twin.Properties = twinProperties;
                        return twinInfo;
                    });
            }

            public async Task UpdateDesiredProperties(string id, TwinCollection patch)
            {
                await this.twinStore.PutOrUpdate(
                    id,
                    new TwinStoreEntity(new Twin(new TwinProperties { Desired = patch })),
                    twinInfo =>
                    {
                        TwinProperties twinProperties = twinInfo.Twin.Properties ?? new TwinProperties();
                        TwinCollection desiredProperties = twinProperties.Desired ?? new TwinCollection();
                        if (desiredProperties.Version + 1 == patch.Version)
                        {
                            string mergedDesiredPropertiesString = JsonEx.Merge(desiredProperties, patch, /* treatNullAsDelete */ true);
                            twinProperties.Desired = new TwinCollection(mergedDesiredPropertiesString);
                            twinInfo.Twin.Properties = twinProperties;
                        }

                        return twinInfo;
                    });
            }

            public async Task Update(string id, Twin twin)
            {
                await this.twinStore.PutOrUpdate(
                    id,
                    new TwinStoreEntity(twin),
                    twinInfo =>
                    {
                        Twin storedTwin = twinInfo.Twin;
                        return (storedTwin.Properties?.Desired?.Version < twin.Properties.Desired.Version
                            && storedTwin.Properties?.Reported?.Version < twin.Properties.Reported.Version)
                            ? new TwinStoreEntity(twin, twinInfo.ReportedPropertiesPatch)
                            : twinInfo;
                    });
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
                    Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                    Option<Twin> twin = await cloudProxy.Map(
                            async cp =>
                            {
                                IMessage twinMessage = await cp.GetTwinAsync();
                                Twin twinValue = this.twinConverter.FromMessage(twinMessage);
                                return Option.Some(twinValue);
                            })
                        .GetOrElse(() => Task.FromResult(Option.None<Twin>()));
                    return twin;
                }
                catch (Exception)
                {
                    return Option.None<Twin>();
                }
            }

            public async Task<bool> UpdateReportedProperties(string id, TwinCollection patch)
            {
                try
                {
                    Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                    bool result = await cloudProxy.Map(
                            async cp =>
                            {
                                IMessage patchMessage = this.twinCollectionConverter.ToMessage(patch);
                                await cp.UpdateReportedPropertiesAsync(patchMessage);
                                return true;
                            })
                        .GetOrElse(() => Task.FromResult(false));
                    return result;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }
}
