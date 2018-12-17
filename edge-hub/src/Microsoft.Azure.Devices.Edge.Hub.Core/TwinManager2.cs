// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using System.Transactions;
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
        readonly IEntityStore<string, TwinInfo2> twinStore;
        readonly ReportedPropertiesStore reportedPropertiesStore;
        readonly TwinStore twinEntityStore;
        readonly CloudSync cloudSync;

        public TwinManager2(IConnectionManager connectionManager,
            IMessageConverter<TwinCollection> twinCollectionConverter,
            IMessageConverter<Twin> twinConverter,
            IEntityStore<string, TwinInfo2> twinStore)
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
            IEntityStore<string, TwinInfo2> twinStore)
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
            Option<Twin> twinOption = await this.cloudSync.TryGetTwin(id);
            Twin twin = await twinOption.Map(
                async t =>
                {
                    await this.twinEntityStore.Update(id, t);
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
            await this.twinEntityStore.UpdateReportedProperties(id, patch);
            await this.reportedPropertiesStore.Update(id, patch);            
            this.reportedPropertiesStore.SyncToCloud(id);
        }

        Task SendPatchToDevice(string id, IMessage twinCollection)
        {
            throw new NotImplementedException();
        }

        class ReportedPropertiesStore
        {
            readonly IEntityStore<string, TwinInfo2> twinStore;
            readonly CloudSync cloudSync;
            readonly ConcurrentDictionary<string, Task> syncToCloudTasks = new ConcurrentDictionary<string, Task>();

            public async Task Update(string id, TwinCollection patch)
            {
                await this.twinStore.PutOrUpdate(
                    id,
                    new TwinInfo2(Option.Some(patch)),
                    twinInfo =>
                    {
                        TwinCollection updatedReportedProperties = twinInfo.ReportedPropertiesPatch
                            .Map(reportedProperties => new TwinCollection(JsonEx.Merge(reportedProperties, patch, /*treatNullAsDelete*/ false)))
                            .GetOrElse(() => patch);
                        return new TwinInfo2(twinInfo.Twin, updatedReportedProperties);
                    });
            }

            public void SyncToCloud(string id)
            {
                if (!this.syncToCloudTasks.TryGetValue(id, out Task currentTask)
                    || currentTask == null
                    || currentTask.IsCompleted)
                {
                    this.syncToCloudTasks[id] = this.SyncToCloudInternal(id);
                }
            }

            async Task SyncToCloudInternal(string id)
            {
                TwinInfo2 twinInfo = await this.twinStore.FindOrPut(id, new TwinInfo2());
                while (twinInfo.ReportedPropertiesPatch.HasValue)
                {
                    // lock
                    twinInfo = await this.twinStore.Update(
                        id,
                        ti => new TwinInfo2(ti.Twin));

                    TwinCollection reportedPropertiesPatch = twinInfo.ReportedPropertiesPatch.Expect(() => new InvalidOperationException("Should have a reported properties patch here"));
                    bool result = await this.cloudSync.UpdateReportedProperties(id, reportedPropertiesPatch);

                    if (!result)
                    {
                        // lock
                        await this.twinStore.Update(
                            id,
                            ti =>
                            {
                                TwinCollection patch = ti.ReportedPropertiesPatch
                                    .Map(rp => new TwinCollection(JsonEx.Merge(reportedPropertiesPatch, rp, /*treatNullAsDelete*/ false)))
                                    .GetOrElse(reportedPropertiesPatch);
                                return new TwinInfo2(ti.Twin, patch);
                            });
                    }
                }
            }
        }

        class TwinStore
        {
            readonly AsyncLockProvider<string> lockProvider;
            readonly IEntityStore<string, TwinInfo2> twinStore;

            public async Task<Twin> Get(string id)
            {
                TwinInfo2 twinInfo = await this.twinStore.FindOrPut(id, new TwinInfo2());
                return twinInfo.Twin;
            }

            public async Task UpdateReportedProperties(string id, TwinCollection patch)
            {
                await this.twinStore.PutOrUpdate(
                    id,
                    new TwinInfo2(new Twin(new TwinProperties { Reported = patch })),
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
                    new TwinInfo2(new Twin(new TwinProperties { Desired = patch })),
                    twinInfo =>
                    {
                        TwinProperties twinProperties = twinInfo.Twin.Properties ?? new TwinProperties();
                        TwinCollection desiredProperties = twinProperties.Desired ?? new TwinCollection();
                        string mergedDesiredPropertiesString = JsonEx.Merge(desiredProperties, patch, /* treatNullAsDelete */ true);
                        twinProperties.Desired = new TwinCollection(mergedDesiredPropertiesString);
                        twinInfo.Twin.Properties = twinProperties;
                        return twinInfo;
                    });
            }

            public async Task Update(string id, Twin twin)
            {
                await this.twinStore.PutOrUpdate(
                    id,
                    new TwinInfo2(twin),
                    twinInfo => new TwinInfo2(twin, twinInfo.ReportedPropertiesPatch));
            }
        }

        class CloudSync
        {
            readonly IConnectionManager connectionManager;
            readonly IMessageConverter<TwinCollection> twinCollectionConverter;
            readonly IMessageConverter<Twin> twinConverter;

            public async Task<Option<Twin>> TryGetTwin(string id)
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
