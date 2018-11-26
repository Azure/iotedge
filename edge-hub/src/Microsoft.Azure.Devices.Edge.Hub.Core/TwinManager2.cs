// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;

    public class TwinManager2 : ITwinManager
    {
        const int TwinPropertyMaxDepth = 5; // taken from IoTHub
        const int TwinPropertyValueMaxLength = 4096; // bytes. taken from IoTHub
        const long TwinPropertyMaxSafeValue = 4503599627370495; // (2^52) - 1. taken from IoTHub
        const long TwinPropertyMinSafeValue = -4503599627370496; // -2^52. taken from IoTHub
        const int TwinPropertyDocMaxLength = 8 * 1024; // 8K bytes. taken from IoTHub
        readonly IMessageConverter<TwinCollection> twinCollectionConverter;
        readonly IMessageConverter<Twin> twinConverter;
        readonly IConnectionManager connectionManager;
        readonly AsyncLock reportedPropertiesLock;
        readonly AsyncLock twinLock;
        readonly ActionBlock<IIdentity> actionBlock;
        readonly IEntityStore<string, TwinInfo> twinStore;

        public TwinManager2(IConnectionManager connectionManager,
            IMessageConverter<TwinCollection> twinCollectionConverter,
            IMessageConverter<Twin> twinConverter,
            IEntityStore<string, TwinInfo> twinStore)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.twinCollectionConverter = Preconditions.CheckNotNull(twinCollectionConverter, nameof(twinCollectionConverter));
            this.twinConverter = Preconditions.CheckNotNull(twinConverter, nameof(twinConverter));
            this.twinStore = Preconditions.CheckNotNull(twinStore, nameof(twinStore));
            this.reportedPropertiesLock = new AsyncLock();
            this.twinLock = new AsyncLock();
            //this.actionBlock = new ActionBlock<IIdentity>(this.ProcessConnectionEstablishedForDevice);
        }

        public static ITwinManager CreateTwinManager(
            IConnectionManager connectionManager,
            IMessageConverterProvider messageConverterProvider,
            IEntityStore<string, TwinInfo> twinStore)
        {
            Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            Preconditions.CheckNotNull(twinStore, nameof(twinStore));
            var twinManager = new TwinManager2(connectionManager, messageConverterProvider.Get<TwinCollection>(), messageConverterProvider.Get<Twin>(),
                twinStore);
            //connectionManager.CloudConnectionEstablished += twinManager.ConnectionEstablishedCallback;
            return twinManager;
        }

        public async Task<IMessage> GetTwinAsync(string id)
        {
            Option<IMessage> twin = Option.None<IMessage>();
            try
            {
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                twin = await cloudProxy.Map(
                    async c =>
                    {
                        IMessage t = await c.GetTwinAsync();
                        await this.AddTwinToStore(this.twinConverter.FromMessage(t), id);
                        return Option.Some(t);
                    }).GetOrElse(() => Task.FromResult(Option.None<IMessage>()));
            }
            catch (Exception)
            {
                // Log exception getting twin from IoThub
            }

            IMessage result = await twin.Map(t => Task.FromResult(t)).GetOrElse(this.GetTwinFromStore(id));
            return result;
        }

        public async Task UpdateDesiredPropertiesAsync(string id, IMessage twinCollection)
        {
            try
            {
                await this.UpdateStoredDesiredProperties(id, this.twinCollectionConverter.FromMessage(twinCollection));
                Option<IDeviceProxy> deviceProxy = this.connectionManager.GetDeviceConnection(id);
                await deviceProxy.ForEachAsync(d => d.OnDesiredPropertyUpdates(twinCollection));
            }
            catch (Exception)
            {
                // Log
            }
        }

        public async Task UpdateReportedPropertiesAsync(string id, IMessage twinCollection)
        {
            bool updatedReportedProperties = false;
            try
            {
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                updatedReportedProperties = await cloudProxy.Map(
                    async c =>
                    {
                        try
                        {
                            await c.UpdateReportedPropertiesAsync(twinCollection);
                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }).GetOrElse(() => Task.FromResult(false));
            }
            catch (Exception )
            {
                // Log
            }

            TwinCollection reportedProperties = this.twinCollectionConverter.FromMessage(twinCollection);
            await this.UpdateReportedPropertiesInStore(id, reportedProperties, updatedReportedProperties);
        }

        async Task UpdateReportedPropertiesInStore(string id, TwinCollection reportedProperties, bool addReportedProperties)
        {
            var putValue = new TwinInfo(new Twin(new TwinProperties {Reported = reportedProperties}), addReportedProperties ? reportedProperties : null);

            Twin MergeWithTwin(Twin storedTwinInfo)
            {
                string mergedJson = JsonEx.Merge(storedTwinInfo.Properties.Reported, reportedProperties, /*treatNullAsDelete*/ true);
                var mergedReportedProperties = new TwinCollection(mergedJson);
                storedTwinInfo.Properties.Reported = mergedReportedProperties;
                return storedTwinInfo;
            }

            TwinInfo Updator(TwinInfo storedTwinInfo)
            {
                Twin twin = storedTwinInfo.Twin != null ? MergeWithTwin(storedTwinInfo.Twin) : putValue.Twin;

                var twinInfo = new TwinInfo(twin,);
            }
            await this.twinStore.PutOrUpdate(
                id,
                putValue,

            );
        }

        async Task<IMessage> GetTwinFromStore(string id)
        {
            TwinInfo twinInfo = await this.twinStore.FindOrPut(id, new TwinInfo(new Twin(), null));
            return this.twinConverter.ToMessage(twinInfo.Twin);
        }

        Task AddTwinToStore(Twin twin, string id) =>
            this.twinStore.PutOrUpdate(id, new TwinInfo(twin, null), t => new TwinInfo(twin, t.ReportedPropertiesPatch));
    }
}
