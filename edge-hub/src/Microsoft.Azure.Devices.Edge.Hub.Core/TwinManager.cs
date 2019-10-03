// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using JetBrains.Annotations;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    public class TwinManager : ITwinManager
    {
        const int TwinPropertyMaxDepth = 5; // taken from IoTHub
        const int TwinPropertyValueMaxLength = 4096; // bytes. taken from IoTHub
        const long TwinPropertyMaxSafeValue = 4503599627370495; // (2^52) - 1. taken from IoTHub
        const long TwinPropertyMinSafeValue = -4503599627370496; // -2^52. taken from IoTHub
        const int TwinPropertyDocMaxLength = 8 * 1024; // 8K bytes. taken from IoTHub
        readonly IMessageConverter<TwinCollection> twinCollectionConverter;
        readonly IMessageConverter<Shared.Twin> twinConverter;
        readonly IConnectionManager connectionManager;
        readonly AsyncLock reportedPropertiesLock;
        readonly AsyncLock twinLock;
        readonly ActionBlock<IIdentity> actionBlock;

        public TwinManager(IConnectionManager connectionManager, IMessageConverter<TwinCollection> twinCollectionConverter, IMessageConverter<Shared.Twin> twinConverter, Option<IEntityStore<string, TwinInfo>> twinStore)
        {
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.twinCollectionConverter = Preconditions.CheckNotNull(twinCollectionConverter, nameof(twinCollectionConverter));
            this.twinConverter = Preconditions.CheckNotNull(twinConverter, nameof(twinConverter));
            this.TwinStore = Preconditions.CheckNotNull(twinStore, nameof(twinStore));
            this.reportedPropertiesLock = new AsyncLock();
            this.twinLock = new AsyncLock();
            this.actionBlock = new ActionBlock<IIdentity>(this.ProcessConnectionEstablishedForDevice);
            Events.Initialized();
        }

        internal Option<IEntityStore<string, TwinInfo>> TwinStore { get; }

        public static ITwinManager CreateTwinManager(
            IConnectionManager connectionManager,
            IMessageConverterProvider messageConverterProvider,
            Option<IStoreProvider> storeProvider)
        {
            Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            Preconditions.CheckNotNull(storeProvider, nameof(storeProvider));
            var twinManager = new TwinManager(
                connectionManager,
                messageConverterProvider.Get<TwinCollection>(),
                messageConverterProvider.Get<Shared.Twin>(),
                storeProvider.Match(
                    s => Option.Some(s.GetEntityStore<string, TwinInfo>(Constants.TwinStorePartitionKey)),
                    () => Option.None<IEntityStore<string, TwinInfo>>()));
            connectionManager.CloudConnectionEstablished += twinManager.ConnectionEstablishedCallback;
            return twinManager;
        }

        public async Task<IMessage> GetTwinAsync(string id)
        {
            return await this.TwinStore.Match(
                async (store) =>
                {
                    TwinInfo twinInfo = await this.GetTwinInfoWithStoreSupportAsync(id);
                    return twinInfo.Twin != null
                        ? this.twinConverter.ToMessage(twinInfo.Twin)
                        : throw new InvalidOperationException($"Error getting twin for device {id}. Twin is null.");
                },
                async () =>
                {
                    // pass through to cloud proxy
                    Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                    return await cloudProxy.Match(async (cp) => await cp.GetTwinAsync(), () => throw new InvalidOperationException($"Cloud proxy unavailable for device {id}"));
                });
        }

        public async Task UpdateDesiredPropertiesAsync(string id, IMessage desiredProperties)
        {
            await this.TwinStore.Map(
                s => this.UpdateDesiredPropertiesWithStoreSupportAsync(id, desiredProperties)).GetOrElse(
                () => this.SendDesiredPropertiesToDeviceProxy(id, desiredProperties));
        }

        public async Task UpdateReportedPropertiesAsync(string id, IMessage reportedProperties)
        {
            if (!this.TwinStore.HasValue)
            {
                await this.SendReportedPropertiesToCloudProxy(id, reportedProperties);
            }
            else
            {
                await this.UpdateReportedPropertiesWithStoreSupportAsync(id, reportedProperties);
            }
        }

        internal static void ValidateTwinProperties(JToken properties) => ValidateTwinProperties(properties, 1);

        // TODO: Move to a Twin helper class (along with Twin manager update).
        internal static string EncodeTwinKey(string key)
        {
            Preconditions.CheckNonWhiteSpace(key, nameof(key));
            var sb = new StringBuilder();
            foreach (char ch in key)
            {
                switch (ch)
                {
                    case '.':
                        sb.Append("%2E");
                        break;

                    case '$':
                        sb.Append("%24");
                        break;

                    case ' ':
                        sb.Append("%20");
                        break;

                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }

        internal void ConnectionEstablishedCallback(object sender, IIdentity identity)
        {
            Events.ConnectionEstablished(identity.Id);
            this.actionBlock.Post(identity);
        }

        internal async Task ExecuteOnTwinStoreResultAsync(string id, Func<TwinInfo, Task> twinStoreHit, Func<Task> twinStoreMiss)
        {
            Option<TwinInfo> cached = await this.TwinStore.Match(s => s.Get(id), () => throw new InvalidOperationException("Missing twin store"));

            if (!cached.HasValue)
            {
                await twinStoreMiss();
            }
            else
            {
                await cached.ForEachAsync(c => twinStoreHit(c));
            }
        }

        internal async Task<TwinInfo> GetTwinInfoWhenCloudOnlineAsync(string id, ICloudProxy cp, bool sendDesiredPropertyUpdate)
        {
            TwinCollection diff = null;
            // Used for returning value to caller
            TwinInfo cached;

            using (await this.twinLock.LockAsync())
            {
                IMessage twinMessage = await cp.GetTwinAsync();
                Shared.Twin cloudTwin = this.twinConverter.FromMessage(twinMessage);
                Events.GotTwinFromCloudSuccess(id, cloudTwin.Properties.Desired.Version, cloudTwin.Properties.Reported.Version);
                var newTwin = new TwinInfo(cloudTwin, null);
                cached = newTwin;

                IEntityStore<string, TwinInfo> twinStore = this.TwinStore.Expect(() => new InvalidOperationException("Missing twin store"));

                await twinStore.PutOrUpdate(
                    id,
                    newTwin,
                    t =>
                    {
                        // If the new twin is more recent than the cached twin, update the cached copy.
                        // If not, reject the cloud twin
                        if (t.Twin == null ||
                            cloudTwin.Properties.Desired.Version > t.Twin.Properties.Desired.Version ||
                            cloudTwin.Properties.Reported.Version > t.Twin.Properties.Reported.Version)
                        {
                            if (t.Twin != null)
                            {
                                Events.UpdateCachedTwin(
                                    id,
                                    t.Twin.Properties.Desired.Version,
                                    cloudTwin.Properties.Desired.Version,
                                    t.Twin.Properties.Reported.Version,
                                    cloudTwin.Properties.Reported.Version);
                                cached = new TwinInfo(cloudTwin, t.ReportedPropertiesPatch);
                                // If the device is subscribed to desired property updates and we are refreshing twin as a result
                                // of a connection reset or desired property update, send a patch to the downstream device
                                if (sendDesiredPropertyUpdate)
                                {
                                    Option<IReadOnlyDictionary<DeviceSubscription, bool>> subscriptions = this.connectionManager.GetSubscriptions(id);
                                    subscriptions.ForEach(
                                        s =>
                                        {
                                            if (s.TryGetValue(DeviceSubscription.DesiredPropertyUpdates, out bool hasDesiredPropertyUpdatesSubscription)
                                                && hasDesiredPropertyUpdatesSubscription)
                                            {
                                                Events.SendDesiredPropertyUpdateToSubscriber(
                                                    id,
                                                    t.Twin.Properties.Desired.Version,
                                                    cloudTwin.Properties.Desired.Version);
                                                diff = new TwinCollection(JsonEx.Diff(t.Twin.Properties.Desired, cloudTwin.Properties.Desired));
                                            }
                                        });
                                }
                            }
                        }
                        else
                        {
                            Events.PreserveCachedTwin(
                                id,
                                t.Twin.Properties.Desired.Version,
                                cloudTwin.Properties.Desired.Version,
                                t.Twin.Properties.Reported.Version,
                                cloudTwin.Properties.Reported.Version);
                            cached = t;
                        }

                        return cached;
                    });
            }

            if ((diff != null) && (diff.Count != 0))
            {
                Events.SendDiffToDeviceProxy(diff.ToString(), id);
                IMessage message = this.twinCollectionConverter.ToMessage(diff);
                await this.SendDesiredPropertiesToDeviceProxy(id, message);
            }

            return cached;
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

        static void ValidateTwinProperties(JToken properties, int currentDepth)
        {
            foreach (JProperty kvp in ((JObject)properties).Properties())
            {
                ValidatePropertyNameAndLength(kvp.Name);

                ValidateValueType(kvp.Name, kvp.Value);

                if (kvp.Value is JValue)
                {
                    if (kvp.Value.Type is JTokenType.Integer)
                    {
                        ValidateIntegerValue(kvp.Name, (long)kvp.Value);
                    }
                    else
                    {
                        string s = kvp.Value.ToString();
                        ValidatePropertyValueLength(kvp.Name, s);
                    }
                }

                if (kvp.Value != null && kvp.Value is JObject)
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

        async Task ProcessConnectionEstablishedForDevice(IIdentity identity)
        {
            try
            {
                // Report pending reported properties up to the cloud
                Events.ProcessConnectionEstablishedForDevice(identity.Id);
                using (await this.reportedPropertiesLock.LockAsync())
                {
                    await this.TwinStore.ForEachAsync(
                        async (store) =>
                        {
                            Option<TwinInfo> twinInfo = await store.Get(identity.Id);
                            await twinInfo.ForEachAsync(
                                async (t) =>
                                {
                                    if (t.ReportedPropertiesPatch.Count != 0)
                                    {
                                        IMessage reported = this.twinCollectionConverter.ToMessage(t.ReportedPropertiesPatch);
                                        await this.SendReportedPropertiesToCloudProxy(identity.Id, reported);
                                        await store.Update(identity.Id, u => new TwinInfo(u.Twin, null));
                                        Events.ReportedPropertiesSyncedToCloudSuccess(identity.Id, t.ReportedPropertiesPatch.Version);
                                    }
                                });
                        });
                }

                // Refresh local copy of the twin
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(identity.Id);
                await cloudProxy.ForEachAsync(
                    async cp =>
                    {
                        Events.GetTwinOnEstablished(identity.Id);
                        await this.GetTwinInfoWhenCloudOnlineAsync(identity.Id, cp, true /* send update to device */);
                    });
            }
            catch (Exception e)
            {
                Events.ConnectionEstablishedCallbackException(identity.Id, e);
            }
        }

        async Task<TwinInfo> GetTwinInfoWithStoreSupportAsync(string id)
        {
            try
            {
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                return await cloudProxy.Map(
                    cp => this.GetTwinInfoWhenCloudOnlineAsync(id, cp, false)).GetOrElse(
                    () => this.GetTwinInfoWhenCloudOfflineAsync(id, new InvalidOperationException($"Error accessing cloud proxy for device {id}")));
            }
            catch (Exception e)
            {
                return await this.GetTwinInfoWhenCloudOfflineAsync(id, e);
            }
        }

        async Task SendDesiredPropertiesToDeviceProxy(string id, IMessage desired)
        {
            IDeviceProxy deviceProxy = this.connectionManager.GetDeviceConnection(id)
                .Expect(() => new InvalidOperationException($"Device proxy unavailable for device {id}"));
            await deviceProxy.OnDesiredPropertyUpdates(desired);
            TwinCollection patch = this.twinCollectionConverter.FromMessage(desired);
            Events.SentDesiredPropertiesToDevice(id, patch.Version);
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
                IEntityStore<string, TwinInfo> twinStore = this.TwinStore.Expect(() => new InvalidOperationException("Missing twin store"));

                await twinStore.Update(
                    id,
                    u =>
                    {
                        // Save the patch only if it is the next one that can be applied
                        if (desired.Version == u.Twin.Properties.Desired.Version + 1)
                        {
                            Events.InOrderDesiredPropertyPatchReceived(
                                id,
                                u.Twin.Properties.Desired.Version,
                                desired.Version);
                            string mergedJson = JsonEx.Merge(u.Twin.Properties.Desired, desired, /*treatNullAsDelete*/ true);
                            u.Twin.Properties.Desired = new TwinCollection(mergedJson);
                        }
                        else
                        {
                            Events.OutOfOrderDesiredPropertyPatchReceived(
                                id,
                                u.Twin.Properties.Desired.Version,
                                desired.Version);
                            getTwin = true;
                        }

                        return new TwinInfo(u.Twin, u.ReportedPropertiesPatch);
                    });
            }

            // Refresh local copy of the twin since we received an out-of-order patch
            if (getTwin)
            {
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
                await cloudProxy.ForEachAsync(cp => this.GetTwinInfoWhenCloudOnlineAsync(id, cp, true /* send update to device */));
            }
            else
            {
                await this.SendDesiredPropertiesToDeviceProxy(id, message);
            }
        }

        async Task UpdateDesiredPropertiesWhenTwinStoreNeedsTwinAsync(string id, TwinCollection desired)
        {
            await this.GetTwinInfoWithStoreSupportAsync(id);
            await this.UpdateDesiredPropertiesWhenTwinStoreHasTwinAsync(id, desired);
        }

        async Task<TwinInfo> GetTwinInfoWhenCloudOfflineAsync(string id, Exception e)
        {
            TwinInfo twinInfo = null;
            await this.ExecuteOnTwinStoreResultAsync(
                id,
                t =>
                {
                    twinInfo = t;
                    Events.GetTwinFromStoreWhenOffline(id, twinInfo, e);
                    return Task.CompletedTask;
                },
                () => throw new InvalidOperationException($"Error getting twin for device {id}", e));
            return twinInfo;
        }

        async Task UpdateReportedPropertiesWhenTwinStoreHasTwinAsync(string id, TwinCollection reported, bool cloudVerified)
        {
            using (await this.twinLock.LockAsync())
            {
                IEntityStore<string, TwinInfo> twinStore = this.TwinStore.Expect(() => new InvalidOperationException("Missing twin store"));
                await twinStore.Update(
                    id,
                    u =>
                    {
                        if (u.Twin == null)
                        {
                            if (!cloudVerified)
                            {
                                ValidateTwinCollectionSize(reported);
                            }

                            var twinProperties = new TwinProperties
                            {
                                Desired = new TwinCollection(),
                                Reported = reported
                            };
                            var twin = new Shared.Twin(twinProperties);
                            Events.UpdatedCachedReportedProperties(id, reported.Version, cloudVerified);
                            return new TwinInfo(twin, reported);
                        }
                        else
                        {
                            string mergedJson = JsonEx.Merge(u.Twin.Properties.Reported, reported, /*treatNullAsDelete*/ true);
                            var mergedReportedProperties = new TwinCollection(mergedJson);

                            if (!cloudVerified)
                            {
                                ValidateTwinCollectionSize(mergedReportedProperties);
                            }

                            u.Twin.Properties.Reported = mergedReportedProperties;
                            Events.UpdatedCachedReportedProperties(id, mergedReportedProperties.Version, cloudVerified);
                            return u;
                        }
                    });
            }
        }

        async Task UpdateReportedPropertiesWhenTwinStoreNeedsTwinAsync(string id, TwinCollection reported, bool cloudVerified)
        {
            try
            {
                await this.GetTwinInfoWithStoreSupportAsync(id);
            }
            catch (Exception e)
            {
                // If we fail to find the twin in the twin store, then we simply store the reported property
                // patch and wait for the next GetTwin or ConnectionEstablished callback to fetch the twin
                Events.MissingTwinOnUpdateReported(id, e);
                throw new TwinNotFoundException("Twin unavailable", e);
            }

            await this.UpdateReportedPropertiesWhenTwinStoreHasTwinAsync(id, reported, cloudVerified);
        }

        async Task UpdateReportedPropertiesPatchAsync(string id, TwinInfo newTwinInfo, TwinCollection reportedProperties)
        {
            try
            {
                using (await this.twinLock.LockAsync())
                {
                    IEntityStore<string, TwinInfo> twinStore = this.TwinStore.Expect(() => new InvalidOperationException("Missing twin store"));

                    await twinStore.PutOrUpdate(
                        id,
                        newTwinInfo,
                        u =>
                        {
                            string mergedJson = JsonEx.Merge(u.ReportedPropertiesPatch, reportedProperties, /*treatNullAsDelete*/ false);
                            var mergedPatch = new TwinCollection(mergedJson);
                            Events.UpdatingReportedPropertiesPatchCollection(id, mergedPatch.Version);
                            return new TwinInfo(u.Twin, mergedPatch);
                        });
                }
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Error updating twin patch for device {id}", e);
            }
        }

        async Task UpdateReportedPropertiesWithStoreSupportAsync(string id, IMessage reportedProperties)
        {
            try
            {
                using (await this.reportedPropertiesLock.LockAsync())
                {
                    bool updatePatch;
                    bool cloudVerified = false;
                    IEntityStore<string, TwinInfo> twinStore = this.TwinStore.Expect(() => new InvalidOperationException("Missing twin store"));

                    Option<TwinInfo> info = await twinStore.Get(id);
                    // If the reported properties patch is not null, we will not attempt to write the reported
                    // properties to the cloud as we are still waiting for a connection established callback
                    // to sync the local reported properties with that of the cloud
                    updatePatch = info.Map(
                        (ti) =>
                        {
                            if (ti.ReportedPropertiesPatch.Count != 0)
                            {
                                Events.NeedsUpdateCachedReportedPropertiesPatch(id, ti.ReportedPropertiesPatch.Version);
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }).GetOrElse(false);

                    TwinCollection reported = this.twinCollectionConverter.FromMessage(reportedProperties);

                    if (!updatePatch)
                    {
                        try
                        {
                            await this.SendReportedPropertiesToCloudProxy(id, reportedProperties);
                            Events.SentReportedPropertiesToCloud(id, reported.Version);
                            cloudVerified = true;
                        }
                        catch (Exception e)
                        {
                            Events.UpdateReportedToCloudException(id, e);
                            updatePatch = true;
                        }
                    }

                    if (!cloudVerified)
                    {
                        ValidateTwinProperties(JToken.Parse(reported.ToJson()), 1);
                        Events.ValidatedTwinPropertiesSuccess(id, reported.Version);
                    }

                    // Update the local twin's reported properties and swallow the exception if we failed
                    // to cache the twin
                    try
                    {
                        await this.ExecuteOnTwinStoreResultAsync(
                            id,
                            (t) => this.UpdateReportedPropertiesWhenTwinStoreHasTwinAsync(id, reported, cloudVerified),
                            () => this.UpdateReportedPropertiesWhenTwinStoreNeedsTwinAsync(id, reported, cloudVerified));
                    }
                    catch (TwinNotFoundException)
                    {
                    }

                    if (updatePatch)
                    {
                        // Update the collective patch of reported properties
                        await this.UpdateReportedPropertiesPatchAsync(
                            id,
                            new TwinInfo(null, reported) /* only used when twin was not previously cached */,
                            reported);
                    }
                }
            }
            catch (Exception e)
            {
                Events.UpdateReportedPropertiesFailed(id, e);
                throw; /* we only throw if we were unable to write to the db */
            }
        }

        async Task SendReportedPropertiesToCloudProxy(string id, IMessage reported)
        {
            Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(id);
            if (!cloudProxy.HasValue)
            {
                throw new InvalidOperationException($"Cloud proxy unavailable for device {id}");
            }

            await cloudProxy.ForEachAsync(cp => cp.UpdateReportedPropertiesAsync(reported));
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.TwinManager;
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinManager>();

            enum EventIds
            {
                UpdateReportedToCloudException = IdStart,
                ReportedPropertiesSyncedToCloudSuccess,
                ValidatedTwinPropertiesSuccess,
                SentReportedPropertiesToCloud,
                NeedsUpdateCachedReportedPropertiesPatch,
                UpdatingReportedPropertiesPatchCollection,
                UpdatedCachedReportedProperties,
                GetTwinFromStoreWhenOffline,
                GotTwinFromCloudSuccess,
                UpdateCachedTwin,
                SendDesiredPropertyUpdateToSubscriber,
                PreserveCachedTwin,
                ConnectionEstablished,
                GetTwinOnEstablished,
                SendDiffToDeviceProxy,
                ProcessConnectionEstablishedForDevice,
                SentDesiredPropertiesToDevice,
                InOrderDesiredPropertyPatchReceived,
                OutOfOrderDesiredPropertyPatchReceived,
                ConnectionEstablishedCallbackException,
                MissingTwinOnUpdateReported,
                UpdateReportedPropertiesFailed,
                Initialized
            }

            public static void UpdateReportedToCloudException(string identity, Exception e)
            {
                Log.LogInformation((int)EventIds.UpdateReportedToCloudException, $"Updating reported properties for {identity} in cloud failed with error {e.GetType()} {e.Message}");
            }

            public static void ReportedPropertiesSyncedToCloudSuccess(string identity, long version)
            {
                Log.LogInformation((int)EventIds.ReportedPropertiesSyncedToCloudSuccess, $"Synced cloud's reported properties at version {version} with edge for {identity}");
            }

            public static void ValidatedTwinPropertiesSuccess(string id, long version)
            {
                Log.LogDebug(
                    (int)EventIds.ValidatedTwinPropertiesSuccess,
                    "Successfully validated reported properties of " +
                    $"twin with id {id} and reported properties version {version}");
            }

            public static void SentReportedPropertiesToCloud(string id, long version)
            {
                Log.LogDebug(
                    (int)EventIds.SentReportedPropertiesToCloud,
                    "Successfully sent reported properties to cloud " +
                    $"for {id} and reported properties version {version}");
            }

            public static void NeedsUpdateCachedReportedPropertiesPatch(string id, long version)
            {
                Log.LogDebug(
                    (int)EventIds.NeedsUpdateCachedReportedPropertiesPatch,
                    "Collective reported properties needs " +
                    $"update for {id} and reported properties version {version}");
            }

            public static void UpdatingReportedPropertiesPatchCollection(string id, long version)
            {
                Log.LogDebug(
                    (int)EventIds.UpdatingReportedPropertiesPatchCollection,
                    "Updating collective reported properties " +
                    $"patch for {id} at version {version}");
            }

            public static void UpdatedCachedReportedProperties(string id, long reportedVersion, bool cloudVerified)
            {
                Log.LogDebug(
                    (int)EventIds.UpdatedCachedReportedProperties,
                    $"Updated cached reported property for {id} " +
                    $"at reported property version {reportedVersion} cloudVerified {cloudVerified}");
            }

            public static void GetTwinFromStoreWhenOffline(string id, TwinInfo twinInfo, Exception e)
            {
                if (twinInfo.Twin != null)
                {
                    Log.LogDebug(
                        (int)EventIds.GetTwinFromStoreWhenOffline,
                        $"Getting twin for {id} at desired version " +
                        $"{twinInfo.Twin.Properties.Desired.Version} reported version {twinInfo.Twin.Properties.Reported.Version} from local store. Get from cloud threw {e.GetType()} {e.Message}");
                }
                else
                {
                    Log.LogDebug((int)EventIds.GetTwinFromStoreWhenOffline, $"Getting twin info for {id}, but twin is null. Get from cloud threw {e.GetType()} {e.Message}");
                }
            }

            public static void GotTwinFromCloudSuccess(string id, long desiredVersion, long reportedVersion)
            {
                Log.LogDebug(
                    (int)EventIds.GotTwinFromCloudSuccess,
                    $"Successfully got twin for {id} from cloud at " +
                    $"desired version {desiredVersion} reported version {reportedVersion}");
            }

            public static void UpdateCachedTwin(string id, long cachedDesired, long cloudDesired, long cachedReported, long cloudReported)
            {
                Log.LogDebug(
                    (int)EventIds.UpdateCachedTwin,
                    $"Updating cached twin for {id} from " +
                    $"desired version {cachedDesired} to {cloudDesired} and reported version {cachedReported} to " +
                    $"{cloudReported}");
            }

            public static void SendDesiredPropertyUpdateToSubscriber(string id, long oldDesiredVersion, long cloudDesiredVersion)
            {
                Log.LogDebug(
                    (int)EventIds.SendDesiredPropertyUpdateToSubscriber,
                    $"Sending desired property update for {id}" +
                    $" old desired version {oldDesiredVersion} cloud desired version {cloudDesiredVersion}");
            }

            public static void PreserveCachedTwin(string id, long cachedDesired, long cloudDesired, long cachedReported, long cloudReported)
            {
                Log.LogDebug(
                    (int)EventIds.PreserveCachedTwin,
                    $"Local twin for {id} at higher or equal desired version " +
                    $"{cachedDesired} compared to cloud {cloudDesired} or reported version {cachedReported} compared to cloud" +
                    $" {cloudReported}");
            }

            public static void ConnectionEstablished(string id)
            {
                Log.LogDebug((int)EventIds.ConnectionEstablished, $"ConnectionEstablished for {id}");
            }

            public static void GetTwinOnEstablished(string id)
            {
                Log.LogDebug((int)EventIds.GetTwinOnEstablished, $"Getting twin for {id} on ConnectionEstablished");
            }

            public static void SendDiffToDeviceProxy(string diff, string id)
            {
                Log.LogDebug((int)EventIds.SendDiffToDeviceProxy, $"Sending diff {diff} to {id}");
            }

            public static void ProcessConnectionEstablishedForDevice(string id)
            {
                Log.LogDebug((int)EventIds.ProcessConnectionEstablishedForDevice, $"Processing ConnectionEstablished for device {id}");
            }

            public static void SentDesiredPropertiesToDevice(string id, long version)
            {
                Log.LogDebug((int)EventIds.SentDesiredPropertiesToDevice, $"Sent desired properties at version {version} to device {id}");
            }

            public static void InOrderDesiredPropertyPatchReceived(string id, long from, long to)
            {
                Log.LogDebug(
                    (int)EventIds.InOrderDesiredPropertyPatchReceived,
                    "In order desired property patch" +
                    $" from {from} to {to} for device {id}");
            }

            public static void OutOfOrderDesiredPropertyPatchReceived(string id, long from, long to)
            {
                Log.LogDebug(
                    (int)EventIds.OutOfOrderDesiredPropertyPatchReceived,
                    "Out of order desired property patch" +
                    $" from {from} to {to} for device {id}");
            }

            public static void ConnectionEstablishedCallbackException(string id, Exception e)
            {
                if (e.HasTimeoutException())
                {
                    Log.LogDebug(
                        (int)EventIds.ConnectionEstablishedCallbackException,
                        $"Timed out while processing connection established callback for client {id} - {e.GetType()} {e.Message}");
                }
                else
                {
                    Log.LogWarning(
                        (int)EventIds.ConnectionEstablishedCallbackException,
                        e,
                        $"Error in connection established callback for client {id}");
                }
            }

            public static void MissingTwinOnUpdateReported(string id, Exception e)
            {
                Log.LogDebug(
                    (int)EventIds.MissingTwinOnUpdateReported,
                    $"Failed to find twin for {id}" +
                    $" while updating reported properties with error {e.Message}");
            }

            public static void UpdateReportedPropertiesFailed(string id, Exception e)
            {
                Log.LogWarning(
                    (int)EventIds.UpdateReportedPropertiesFailed,
                    "Failed to update reported " +
                    $" properties for {id} with error {e.Message}");
            }

            public static void Initialized()
            {
                Log.LogInformation((int)EventIds.Initialized, "Initialized twin manager v1.");
            }
        }
    }
}
