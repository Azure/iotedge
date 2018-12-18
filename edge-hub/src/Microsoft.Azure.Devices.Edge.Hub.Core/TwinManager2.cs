// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Runtime.InteropServices.ComTypes;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using System.Transactions;
    using JetBrains.Annotations;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;

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
        readonly ConcurrentDictionary<string, DateTime> twinSyncTime = new ConcurrentDictionary<string, DateTime>();

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
            IEntityStore<string, TwinInfo2> twinStore,
            IDeviceConnectivityManager deviceConnectivityManager,
            TimeSpan twinSyncPeriod)
        {
            Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            Preconditions.CheckNotNull(twinStore, nameof(twinStore));
            Preconditions.CheckNotNull(deviceConnectivityManager, nameof(deviceConnectivityManager));

            var twinManager = new TwinManager2(connectionManager, messageConverterProvider.Get<TwinCollection>(), messageConverterProvider.Get<Twin>(),
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
            TwinValidator.ValidateReportedProperties(patch);
            await this.twinEntityStore.UpdateReportedProperties(id, patch);
            await this.reportedPropertiesStore.Update(id, patch);
            this.reportedPropertiesStore.InitSyncToCloud(id);
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
            readonly IEntityStore<string, TwinInfo2> twinStore;
            readonly CloudSync cloudSync;
            readonly ConcurrentDictionary<string, Task> syncToCloudTasks = new ConcurrentDictionary<string, Task>();
            readonly AsyncLockProvider<string> lockProvider = new AsyncLockProvider<string>(10);
            readonly AsyncLock syncTaskLock = new AsyncLock();

            public ReportedPropertiesStore(IEntityStore<string, TwinInfo2> twinStore, CloudSync cloudSync)
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
                        new TwinInfo2(Option.Some(patch)),
                        twinInfo =>
                        {
                            TwinCollection updatedReportedProperties = twinInfo.ReportedPropertiesPatch
                                .Map(reportedProperties => new TwinCollection(JsonEx.Merge(reportedProperties, patch, /*treatNullAsDelete*/ false)))
                                .GetOrElse(() => patch);
                            return new TwinInfo2(twinInfo.Twin, updatedReportedProperties);
                        });
                }
            }

            public async Task InitSyncToCloud(string id)
            {
                if (!this.syncToCloudTasks.TryGetValue(id, out Task currentTask)
                    || currentTask.IsCompleted)
                {
                    using (await this.syncTaskLock.LockAsync())
                    {
                        if (!this.syncToCloudTasks.TryGetValue(id, out currentTask)
                            || currentTask.IsCompleted)
                        {
                            this.syncToCloudTasks[id] = this.SyncToCloud(id);                           
                        }
                    }
                }
            }

            public async Task SyncToCloud(string id)
            {
                Option<TwinInfo2> twinInfoOption = await this.twinStore.Get(id);
                await twinInfoOption.ForEachAsync(
                    async twinInfo =>
                    {
                        if (twinInfo.ReportedPropertiesPatch.HasValue)
                        {
                            using (await this.lockProvider.GetLock(id).LockAsync())
                            {
                                twinInfo = await this.twinStore.FindOrPut(id, new TwinInfo2());
                                await twinInfo.ReportedPropertiesPatch.ForEachAsync(
                                    async reportedPropertiesPatch =>
                                    {
                                        bool result = await this.cloudSync.UpdateReportedProperties(id, reportedPropertiesPatch);

                                        if (result)
                                        {
                                            await this.twinStore.Update(
                                                id,
                                                ti => new TwinInfo2(ti.Twin));
                                        }
                                    });
                            }
                        }
                    });
            }
        }

        internal class TwinStore
        {
            readonly IEntityStore<string, TwinInfo2> twinStore;

            public TwinStore(IEntityStore<string, TwinInfo2> twinStore)
            {
                this.twinStore = twinStore;
            }

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
                    new TwinInfo2(twin),
                    twinInfo =>
                    {
                        Twin storedTwin = twinInfo.Twin;
                        return (storedTwin.Properties?.Desired?.Version < twin.Properties.Desired.Version
                            && storedTwin.Properties?.Reported?.Version < twin.Properties.Reported.Version)
                        ? new TwinInfo2(twin, twinInfo.ReportedPropertiesPatch)
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

        internal class TwinValidator
        {
            public static bool ValidateReportedProperties(TwinCollection reportedProperties)
            {
                JToken.Parse(reportedProperties.ToJson());
                return true;
            }

            static void ValidateTwinProperties(JToken properties, int currentDepth)
            {
                foreach (JProperty kvp in ((JObject)properties).Properties())
                {
                    ValidatePropertyNameAndLength(kvp.Name);

                    ValidateValueType(kvp.Name, kvp.Value);

                    string s = kvp.Value.ToString();
                    ValidatePropertyValueLength(kvp.Name, s);

                    if ((kvp.Value is JValue) && (kvp.Value.Type is JTokenType.Integer))
                    {
                        ValidateIntegerValue(kvp.Name, (long)kvp.Value);
                    }

                    if ((kvp.Value != null) && (kvp.Value is JObject))
                    {
                        if (currentDepth > TwinPropertyMaxDepth)
                        {
                            throw new InvalidOperationException($"Nested depth of twin property exceeds {TwinPropertyMaxDepth}");
                        }

                        // do validation recursively
                        ValidateTwinProperties(kvp.Value, currentDepth + 1);
                    }
                }
            }

            static void ValidatePropertyNameAndLength(string name)
            {
                if (name != null && Encoding.UTF8.GetByteCount(name) > TwinPropertyValueMaxLength)
                {
                    string truncated = name.Substring(0, 10);
                    throw new InvalidOperationException($"Length of property name {truncated}.. exceeds maximum length of {TwinPropertyValueMaxLength}");
                }

                // Disabling Possible Null Referece, since name is being tested above.
                // ReSharper disable once PossibleNullReferenceException
                for (int index = 0; index < name.Length; index++)
                {
                    char ch = name[index];
                    // $ is reserved for service properties like $metadata, $version etc.
                    // However, $ is already a reserved character in Mongo, so we need to substitute it with another character like #.
                    // So we're also reserving # for service side usage.
                    if (char.IsControl(ch) || ch == '.' || ch == '$' || ch == '#' || char.IsWhiteSpace(ch))
                    {
                        throw new InvalidOperationException($"Property name {name} contains invalid character '{ch}'");
                    }
                }
            }

            static void ValidatePropertyValueLength(string name, string value)
            {
                int valueByteCount = value != null ? Encoding.UTF8.GetByteCount(value) : 0;
                if (valueByteCount > TwinPropertyValueMaxLength)
                {
                    throw new InvalidOperationException($"Value associated with property name {name} has length {valueByteCount} that exceeds maximum length of {TwinPropertyValueMaxLength}");
                }
            }

            [AssertionMethod]
            static void ValidateIntegerValue(string name, long value)
            {
                if (value > TwinPropertyMaxSafeValue || value < TwinPropertyMinSafeValue)
                {
                    throw new InvalidOperationException($"Property {name} has an out of bound value. Valid values are between {TwinPropertyMinSafeValue} and {TwinPropertyMaxSafeValue}");
                }
            }

            static void ValidateValueType(string property, JToken value)
            {
                if (!JsonEx.IsValidToken(value))
                {
                    throw new InvalidOperationException($"Property {property} has a value of unsupported type. Valid types are integer, float, string, bool, null and nested object");
                }
            }

            static void ValidateTwinCollectionSize(TwinCollection collection)
            {
                long size = Encoding.UTF8.GetByteCount(collection.ToJson());
                if (size > TwinPropertyDocMaxLength)
                {
                    throw new InvalidOperationException($"Twin properties size {size} exceeds maximum {TwinPropertyDocMaxLength}");
                }
            }
        }
    }
}
