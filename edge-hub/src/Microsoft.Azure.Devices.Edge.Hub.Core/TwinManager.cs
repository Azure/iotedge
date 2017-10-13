// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    public class TwinManager : ITwinManager
    {
        readonly IMessageConverter<TwinCollection> twinCollectionConverter;
        readonly IMessageConverter<Twin> twinConverter;
        readonly IConnectionManager connectionManager;
        readonly AsyncLock reportedPropertiesLock;
        readonly AsyncLock twinLock;
        readonly ActionBlock<IIdentity> actionBlock;
        internal Option<IEntityStore<string, TwinInfo>> TwinStore { get; }

        public TwinManager(IConnectionManager connectionManager, IMessageConverter<TwinCollection> twinCollectionConverter, IMessageConverter<Twin> twinConverter, Option<IEntityStore<string, TwinInfo>> twinStore)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.twinCollectionConverter = Preconditions.CheckNotNull(twinCollectionConverter, nameof(twinCollectionConverter));
            this.twinConverter = Preconditions.CheckNotNull(twinConverter, nameof(twinConverter));
            this.TwinStore = Preconditions.CheckNotNull(twinStore, nameof(twinStore));
            this.reportedPropertiesLock = new AsyncLock();
            this.twinLock = new AsyncLock();
            this.actionBlock = new ActionBlock<IIdentity>(this.ProcessConnectionEstablishedForDevice);
        }

        public static ITwinManager CreateTwinManager(IConnectionManager connectionManager, IMessageConverterProvider messageConverterProvider, Option<IStoreProvider> storeProvider)
        {
            Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            Preconditions.CheckNotNull(storeProvider, nameof(storeProvider));
            TwinManager twinManager = new TwinManager(connectionManager, messageConverterProvider.Get<TwinCollection>(), messageConverterProvider.Get<Twin>(),
                storeProvider.Match(
                    s => Option.Some(s.GetEntityStore<string, TwinInfo>(Constants.TwinStorePartitionKey)),
                    () => Option.None<IEntityStore<string, TwinInfo>>()));
            connectionManager.CloudConnectionEstablished += twinManager.ConnectionEstablishedCallback;
            return twinManager;
        }

        async Task ProcessConnectionEstablishedForDevice(IIdentity identity)
        {
            // Report pending reported properties up to the cloud

            using (await this.reportedPropertiesLock.LockAsync())
            {
                await this.TwinStore.Match(
                    async (store) =>
                    {
                        Option<TwinInfo> twinInfo = await store.Get(identity.Id);
                        await twinInfo.Match(
                            async (t) =>
                            {
                                if (t.ReportedPropertiesPatch.Count > 0)
                                {
                                    IMessage reported = this.twinCollectionConverter.ToMessage(t.ReportedPropertiesPatch);
                                    await this.SendReportedPropertiesToCloudProxy(identity.Id, reported);
                                    await store.Update(identity.Id, u => new TwinInfo(u.Twin, null, u.SubscribedToDesiredPropertyUpdates));
                                    Events.ReportedPropertiesUpdateToCloudSuccess(identity.Id);
                                }
                            },
                            () => { return Task.CompletedTask; });
                    },
                    () => { return Task.CompletedTask; }
                    );
            }

            // Refresh local copy of the twin
            Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(identity.Id);
            await cloudProxy.Map<Task>(
                (cp) =>
                    this.GetTwinInfoWhenCloudOnlineAsync(identity.Id, cp, true /* send update to device */)
                ).GetOrElse(Task.CompletedTask);
        }

        internal void ConnectionEstablishedCallback(object sender, IIdentity identity)
        {
            this.actionBlock.Post(identity);
        }

        public async Task<IMessage> GetTwinAsync(string id)
        {
            return await this.TwinStore.Match(
                async (store) =>
                {
                    TwinInfo twinInfo = await this.GetTwinInfoWithStoreSupportAsync(id);
                    return this.twinConverter.ToMessage(twinInfo.Twin);
                },
                async () =>
                {
                    // pass through to cloud proxy
                    Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(id);
                    return await cloudProxy.Match(async (cp) => await cp.GetTwinAsync(), () => throw new InvalidOperationException($"Cloud proxy unavailable for device {id}"));
                });
        }

        async Task<TwinInfo> GetTwinInfoWithStoreSupportAsync(string id)
        {
            try
            {
                Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(id);
                return await cloudProxy.Map(
                        cp => this.GetTwinInfoWhenCloudOnlineAsync(id, cp, false)
                    ).GetOrElse(() => this.GetTwinInfoWhenCloudOfflineAsync(id, new InvalidOperationException($"Error accessing cloud proxy for device {id}")));
            }
            catch (Exception e)
            {
                return await this.GetTwinInfoWhenCloudOfflineAsync(id, e);
            }
        }

        internal async Task ExecuteOnTwinStoreResultAsync(string id, Func<TwinInfo, Task> twinStoreHit, Func<Task> twinStoreMiss)
        {
            Option<TwinInfo> cached = await this.TwinStore.Match(s => s.Get(id), () => throw new InvalidOperationException("Missing twin store"));
            await cached.Match(c => twinStoreHit(c), () => twinStoreMiss());
        }

        public async Task UpdateDesiredPropertiesAsync(string id, IMessage desiredProperties)
        {
            await this.TwinStore.Map(
                    s => this.UpdateDesiredPropertiesWithStoreSupportAsync(id, desiredProperties)
                ).GetOrElse(() => this.SendDesiredPropertiesToDeviceProxy(id, desiredProperties));
        }

        async Task SendDesiredPropertiesToDeviceProxy(string id, IMessage desired)
        {
            Option<IDeviceProxy> deviceProxy = this.connectionManager.GetDeviceConnection(id);
            await deviceProxy.Match(dp => dp.OnDesiredPropertyUpdates(desired), () => throw new InvalidOperationException($"Device proxy unavailable for device {id}"));
        }

        async Task UpdateDesiredPropertiesWithStoreSupportAsync(string id, IMessage desiredProperties)
        {
            try
            {
                TwinCollection desired = this.twinCollectionConverter.FromMessage(desiredProperties);
                await this.ExecuteOnTwinStoreResultAsync(
                    id,
                    t => this.UpdateDesiredPropertiesWhenTwinStoreHasTwinAsync(id, desired),
                    () => this.UpdateDesiredPropertiesWhenTwinStoreNeedsTwinAsync(id, desired));
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Error processing desired properties for device {id}", e);
            }
        }

        async Task UpdateDesiredPropertiesWhenTwinStoreHasTwinAsync(string id, TwinCollection desired)
        {
            bool getTwin = false;
            IMessage message = this.twinCollectionConverter.ToMessage(desired);
            using (await this.twinLock.LockAsync())
            {
                await this.TwinStore.Match(
                    (s) => s.Update(
                        id,
                        u =>
                        {
                            // Save the patch only if it is the next one that can be applied
                            if (desired.Version == u.Twin.Properties.Desired.Version + 1)
                            {
                                u.Twin.Properties.Desired = MergeTwinCollections(u.Twin.Properties.Desired, desired, true);
                            }
                            else
                            {
                                getTwin = true;
                            }
                            return new TwinInfo(u.Twin, u.ReportedPropertiesPatch, true);
                        }),
                    () => throw new InvalidOperationException("Missing twin store"));
            }

            // Refresh local copy of the twin since we received an out-of-order patch
            if (getTwin)
            {
                Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(id);
                await cloudProxy.Map<Task>(
                        cp => this.GetTwinInfoWhenCloudOnlineAsync(id, cp, true /* send update to device */)
                    ).GetOrElse(Task.CompletedTask);
            }
            else
            {
                await this.SendDesiredPropertiesToDeviceProxy(id, message);
            }
        }

        async Task UpdateDesiredPropertiesWhenTwinStoreNeedsTwinAsync(string id, TwinCollection desired)
        {
            TwinInfo twinInfo = await this.GetTwinInfoWithStoreSupportAsync(id);
            await this.UpdateDesiredPropertiesWhenTwinStoreHasTwinAsync(id, desired);
        }

        internal async Task<TwinInfo> GetTwinInfoWhenCloudOnlineAsync(string id, ICloudProxy cp, bool sendDesiredPropertyUpdate)
        {
            TwinCollection diff = null;
            // Used for returning value to caller
            TwinInfo cached = null;

            using (await this.twinLock.LockAsync())
            {
                IMessage twinMessage = await cp.GetTwinAsync();
                Twin cloudTwin = twinConverter.FromMessage(twinMessage);
                TwinInfo newTwin = new TwinInfo(cloudTwin, null, false);
                cached = newTwin;
                await this.TwinStore.Match(
                    (s) => s.PutOrUpdate(
                        id,
                        newTwin,
                        t =>
                        {
                            // If the new twin is more recent than the cached twin, update the cached copy.
                            // If not, reject the cloud twin
                            if (cloudTwin.Version > t.Twin.Version)
                            {
                                cached = new TwinInfo(cloudTwin, t.ReportedPropertiesPatch, t.SubscribedToDesiredPropertyUpdates);
                                // If the device is subscribed to desired property updates and we are refreshing twin as a result
                                // of a connection reset or desired property update, send a patch to the downstream device
                                if (sendDesiredPropertyUpdate && t.SubscribedToDesiredPropertyUpdates)
                                {
                                    diff = DiffTwinCollections(t.Twin.Properties.Desired, cloudTwin.Properties.Desired);
                                }
                            }
                            else
                            {
                                cached = t;
                            }
                            return cached;
                        }),
                    () => throw new InvalidOperationException("Missing twin store"));
            }
            if (diff != null)
            {
                IMessage message = this.twinCollectionConverter.ToMessage(diff);
                await this.SendDesiredPropertiesToDeviceProxy(id, message);
            }
            return cached;
        }

        async Task<TwinInfo> GetTwinInfoWhenCloudOfflineAsync(string id, Exception e)
        {
            TwinInfo twinInfo = null;
            await this.ExecuteOnTwinStoreResultAsync(
                id,
                t =>
                {
                    twinInfo = t;
                    return Task.CompletedTask;
                },
                () => throw new InvalidOperationException($"Error getting twin for device {id}"));
            return twinInfo;
        }

        async Task UpdateReportedPropertiesWhenTwinStoreHasTwinAsync(string id, TwinCollection reported)
        {
            using (await this.twinLock.LockAsync())
            {
                await this.TwinStore.Match(
                    (s) => s.Update(
                        id,
                        u =>
                        {
                            TwinCollection mergedProperty = MergeTwinCollections(u.Twin.Properties.Reported, reported, true /* treatNullAsDelete */);
                            u.Twin.Properties.Reported = mergedProperty;
                            return u;
                        }),
                    () => throw new InvalidOperationException("Missing twin store"));
            }
        }

        async Task UpdateReportedPropertiesWhenTwinStoreNeedsTwinAsync(string id, TwinCollection reported)
        {
            TwinInfo twinInfo = await this.GetTwinInfoWithStoreSupportAsync(id);
            await this.UpdateReportedPropertiesWhenTwinStoreHasTwinAsync(id, reported);
        }

        public async Task UpdateReportedPropertiesAsync(string id, IMessage reportedProperties)
        {
            await this.TwinStore.Match(
                (s) => this.UpdateReportedPropertiesWithStoreSupportAsync(id, reportedProperties),
                () => this.SendReportedPropertiesToCloudProxy(id, reportedProperties));
        }

        async Task UpdateReportedPropertiesPatch(string id, TwinCollection reportedProperties)
        {
            await this.TwinStore.Match(
                (s) => s.Update(
                    id,
                    u =>
                    {
                        TwinCollection mergedPatch = MergeTwinCollections(u.ReportedPropertiesPatch, reportedProperties, false /* treatNullAsDelete */);
                        return new TwinInfo(u.Twin, mergedPatch, u.SubscribedToDesiredPropertyUpdates);
                    }),
                () => throw new InvalidOperationException("Missing twin store"));
        }

        async Task UpdateReportedPropertiesWithStoreSupportAsync(string id, IMessage reportedProperties)
        {
            using (await this.reportedPropertiesLock.LockAsync())
            {
                bool updatePatch = false;
                await this.TwinStore.Match(
                    async (s) =>
                    {
                        Option<TwinInfo> info = await s.Get(id);
                        // If the reported properties patch is not null, we will not attempt to write the reported
                        // properties to the cloud as we are still waiting for a connection established callback
                        // to sync the local reported proeprties with that of the cloud
                        info.Match(ti => updatePatch = (ti.ReportedPropertiesPatch.Count != 0), () => false);
                    },
                    () => throw new InvalidOperationException("Missing twin store")
                    );

                if (!updatePatch)
                {
                    try
                    {
                        await this.SendReportedPropertiesToCloudProxy(id, reportedProperties);
                    }
                    catch (Exception e)
                    {
                        Events.UpdateReportedToCloudException(id, e);
                        updatePatch = true;
                    }
                }

                TwinCollection reported = this.twinCollectionConverter.FromMessage(reportedProperties);

                // Update the local twin's reported properties
                await this.ExecuteOnTwinStoreResultAsync(
                    id,
                    (t) => this.UpdateReportedPropertiesWhenTwinStoreHasTwinAsync(id, reported),
                    () => this.UpdateReportedPropertiesWhenTwinStoreNeedsTwinAsync(id, reported));

                if (updatePatch)
                {
                    // Update the collective patch of reported properties
                    await this.ExecuteOnTwinStoreResultAsync(
                        id,
                        (t) => this.UpdateReportedPropertiesPatch(id, reported),
                        () => throw new InvalidOperationException($"Missing cached twin for device {id}"));
                }
            }
        }

        async Task SendReportedPropertiesToCloudProxy(string id, IMessage reported)
        {
            Option<ICloudProxy> cloudProxy = this.connectionManager.GetCloudConnection(id);
            await cloudProxy.Match(
                async (cp) =>
                {
                    await cp.UpdateReportedPropertiesAsync(reported);
                }, () => throw new InvalidOperationException($"Cloud proxy unavailable for device {id}"));
        }

        internal static TwinCollection MergeTwinCollections(TwinCollection baseline, TwinCollection patch, bool treatNullAsDelete)
        {
            Preconditions.CheckNotNull(baseline, nameof(baseline));
            Preconditions.CheckNotNull(patch, nameof(patch));
            JToken baselineToken = JToken.Parse(baseline.ToJson()).DeepClone();
            JToken patchToken = JToken.Parse(patch.ToJson()).DeepClone();
            return new TwinCollection(MergeTwinCollections(baselineToken, patchToken, treatNullAsDelete).ToJson());
        }

        static JToken MergeTwinCollections(JToken baseline, JToken patch, bool treatNullAsDelete)
        {
            // Reached the leaf JValue
            if ((patch is JValue) || (baseline.Type == JTokenType.Null) || (baseline is JValue))
            {
                return patch;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include
            };

            Dictionary<string, JToken> patchDictionary = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(patch.ToJson(), settings);

            Dictionary<string, JToken> baselineDictionary = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(baseline.ToJson(), settings);

            Dictionary<string, JToken> result = baselineDictionary;
            foreach (KeyValuePair<string, JToken> patchPair in patchDictionary)
            {
                bool baselineContainsKey = baselineDictionary.ContainsKey(patchPair.Key);
                if (baselineContainsKey && (patchPair.Value.Type != JTokenType.Null))
                {
                    JToken baselineValue = baselineDictionary[patchPair.Key];
                    JToken nestedResult = MergeTwinCollections(baselineValue, patchPair.Value, treatNullAsDelete);
                    result[patchPair.Key] = nestedResult;
                }
                else // decide whether to remove or add the patch key
                {
                    if (treatNullAsDelete && (patchPair.Value.Type == JTokenType.Null))
                    {
                        result.Remove(patchPair.Key);
                    }
                    else
                    {
                        result[patchPair.Key] = patchPair.Value;
                    }
                }
            }
            return JToken.FromObject(result);
        }

        internal static TwinCollection DiffTwinCollections(TwinCollection from, TwinCollection to)
        {
            Preconditions.CheckNotNull(from, nameof(from));
            Preconditions.CheckNotNull(to, nameof(to));
            JToken fromToken = JToken.Parse(from.ToJson());
            JToken toToken = JToken.Parse(to.ToJson());
            JToken diff = DiffTwinCollections(fromToken, toToken);
            return diff == null ? new TwinCollection() : new TwinCollection(diff.ToJson());
        }

        static JToken DiffTwinCollections(JToken from, JToken to)
        {
            if ((to is JValue) || (from is JValue))
            {
                return Equals(to, from) ? null : to;
            }

            Dictionary<string, JToken> toDictionary = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(to.ToJson());
            Dictionary<string, JToken> fromDictionary = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(from.ToJson());
            Dictionary<string, JToken> result = toDictionary;

            foreach (KeyValuePair<string, JToken> fromPair in fromDictionary)
            {
                bool toContainsKey = toDictionary.ContainsKey(fromPair.Key);
                if (toContainsKey)
                {
                    JToken nestedResult = DiffTwinCollections(fromPair.Value, toDictionary[fromPair.Key]);
                    if (nestedResult != null)
                    {
                        result[fromPair.Key] = nestedResult;
                    }
                    else
                    {
                        result.Remove(fromPair.Key);
                    }
                }
                else
                {
                    result[fromPair.Key] = null;
                }
            }

            if (result.Count == 0)
            {
                return null;
            }

            return JToken.FromObject(result);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinManager>();
            const int IdStart = HubCoreEventIds.TwinManager;

            enum EventIds
            {
                UpdateReportedToCloudException = IdStart,
                StoreTwinFailed,
                ReportedPropertiesUpdateToCloudSuccess
            }

            public static void UpdateReportedToCloudException(string identity, Exception e)
            {
                Log.LogInformation((int)EventIds.UpdateReportedToCloudException, $"Updating reported properties for {identity} in cloud failed with error {e.GetType()} {e.Message}");
            }

            public static void StoreTwinFailed(string identity, Exception e, long v, long desired, long reported)
            {
                Log.LogDebug((int)EventIds.StoreTwinFailed, $"Storing twin for {identity} failed with error {e.GetType()} {e.Message}. Retrieving last stored twin with version {v}, desired version {desired} and reported version {reported}");
            }

            public static void ReportedPropertiesUpdateToCloudSuccess(string identity)
            {
                Log.LogInformation((int)EventIds.ReportedPropertiesUpdateToCloudSuccess, $"Synced cloud's reported properties with edge for {identity}", identity);
            }
        }
    }
}
