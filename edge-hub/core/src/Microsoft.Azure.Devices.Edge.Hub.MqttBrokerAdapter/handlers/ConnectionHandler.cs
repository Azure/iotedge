// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class ConnectionHandler : IConnectionRegistry, IMessageConsumer
    {
        const string TopicDeviceConnected = "$edgehub/connected";

        static readonly char[] identitySegmentSeparator = new[] { '/' };
        static readonly string[] subscriptions = new[] { TopicDeviceConnected };

        readonly Task<IConnectionProvider> connectionProviderGetter;
        readonly IIdentityProvider identityProvider;
        readonly ISystemComponentIdProvider systemComponentIdProvider;
        readonly DeviceProxy.Factory deviceProxyFactory;

        AsyncLock guard = new AsyncLock();

        // Normal dictionary would be sufficient because of the locks, however we need AddOrUpdate()
        ConcurrentDictionary<IIdentity, IDeviceListener> knownConnections = new ConcurrentDictionary<IIdentity, IDeviceListener>();

        // this class is auto-registered so no way to implement an async activator.
        // hence this one needs to get a Task<T> which is suboptimal, but that is the way
        // IConnectionProvider is registered
        public ConnectionHandler(Task<IConnectionProvider> connectionProviderGetter, IIdentityProvider identityProvider, ISystemComponentIdProvider systemComponentIdProvider, DeviceProxy.Factory deviceProxyFactory)
        {
            this.connectionProviderGetter = Preconditions.CheckNotNull(connectionProviderGetter);
            this.identityProvider = Preconditions.CheckNotNull(identityProvider);
            this.systemComponentIdProvider = Preconditions.CheckNotNull(systemComponentIdProvider);
            this.deviceProxyFactory = Preconditions.CheckNotNull(deviceProxyFactory);
        }

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public async Task<Option<IDeviceListener>> GetDeviceListenerAsync(IIdentity identity)
        {
            using (await this.guard.LockAsync())
            {
                if (this.knownConnections.TryGetValue(identity, out IDeviceListener result))
                {
                    return Option.Some(result);
                }
                else
                {
                    return Option.None<IDeviceListener>();
                }
            }
        }

        public async Task<Option<IDeviceProxy>> GetDeviceProxyAsync(IIdentity identity)
        {
            using (await this.guard.LockAsync())
            {
                if (this.knownConnections.TryGetValue(identity, out var container))
                {
                    if (container is IDeviceProxy result)
                    {
                        return Option.Some(result);
                    }
                }

                return Option.None<IDeviceProxy>();
            }
        }

        public async Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            if (string.Equals(publishInfo.Topic, TopicDeviceConnected))
            {
                try
                {
                    await this.HandleDeviceConnectedAsync(publishInfo);
                    return true;
                }
                catch (Exception e)
                {
                    Events.ErrorProcessingNotification(e);
                    return false;
                }
            }

            return false;
        }

        async Task HandleDeviceConnectedAsync(MqttPublishInfo mqttPublishInfo)
        {
            var updatedIdentities = this.GetIdentitiesFromUpdateMessage(mqttPublishInfo);
            await updatedIdentities.ForEachAsync(async i => await this.ReconcileConnectionsAsync(new HashSet<IIdentity>(i)));
        }

        async Task ReconcileConnectionsAsync(HashSet<IIdentity> updatedIdentities)
        {
            using (await this.guard.LockAsync())
            {
                var identitiesAdded = default(HashSet<IIdentity>);
                var identitiesRemoved = default(HashSet<IIdentity>);
                var knownIdentities = this.knownConnections.Keys;

                identitiesAdded = new HashSet<IIdentity>(updatedIdentities);
                identitiesAdded.ExceptWith(knownIdentities);

                identitiesRemoved = new HashSet<IIdentity>(knownIdentities);
                identitiesRemoved.ExceptWith(updatedIdentities);

                await this.RemoveConnectionsAsync(identitiesRemoved);
                await this.AddConnectionsAsync(identitiesAdded);
            }
        }

        async Task RemoveConnectionsAsync(HashSet<IIdentity> identitiesRemoved)
        {
            foreach (var identity in identitiesRemoved)
            {
                if (this.knownConnections.TryRemove(identity, out var deviceListener))
                {
                    await deviceListener.CloseAsync();
                }
                else
                {
                    Events.UnknownClientDisconnected(identity.Id);
                }
            }
        }

        async Task AddConnectionsAsync(HashSet<IIdentity> identitiesAdded)
        {
            var connectionProvider = await this.connectionProviderGetter;

            if (connectionProvider == null)
            {
                Events.FailedToObtainConnectionProvider();
                return;
            }

            foreach (var identity in identitiesAdded)
            {
                var deviceListener = await connectionProvider.GetDeviceListenerAsync(identity, Option.None<string>());
                var deviceProxy = this.deviceProxyFactory(identity);

                deviceListener.BindDeviceProxy(deviceProxy);

                var previousListener = default(IDeviceListener);

                this.knownConnections.AddOrUpdate(
                    identity,
                    deviceListener,
                    (k, v) =>
                    {
                        previousListener = v;
                        return deviceListener;
                    });

                if (previousListener != null)
                {
                    Events.ExistingClientAdded(previousListener.Identity.Id);
                    await previousListener.CloseAsync();
                }
            }
        }

        Option<List<IIdentity>> GetIdentitiesFromUpdateMessage(MqttPublishInfo mqttPublishInfo)
        {
            var identityList = default(List<string>);

            try
            {
                var payloadAsString = Encoding.UTF8.GetString(mqttPublishInfo.Payload);
                identityList = JsonConvert.DeserializeObject<List<string>>(payloadAsString);
            }
            catch (Exception e)
            {
                Events.BadPayloadFormat(e);
                return Option.None<List<IIdentity>>();
            }

            var result = new List<IIdentity>();

            foreach (var id in identityList)
            {
                if (this.IsSystemComponent(id))
                {
                    continue;
                }

                var identityComponents = id.Split(identitySegmentSeparator, StringSplitOptions.RemoveEmptyEntries);

                switch (identityComponents.Length)
                {
                    case 1:
                        result.Add(this.identityProvider.Create(identityComponents[0]));
                        break;

                    case 2:
                        result.Add(this.identityProvider.Create(identityComponents[0], identityComponents[1]));
                        break;

                    default:
                        Events.BadIdentityFormat(id);
                        continue;
                }
            }

            return Option.Some(result);
        }

        bool IsSystemComponent(string id) => string.Equals(id, this.systemComponentIdProvider.EdgeHubBridgeId);

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.ConnectionHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ConnectionHandler>();

            enum EventIds
            {
                BadPayloadFormat = IdStart,
                BadIdentityFormat,
                UnknownClientDisconnected,
                ExistingClientAdded,
                BlockingDependencyInjection,
                ErrorProcessingNotification,
                FailedToObtainConnectionProvider
            }

            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize connection update");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void UnknownClientDisconnected(string identity) => Log.LogWarning((int)EventIds.UnknownClientDisconnected, $"Received disconnect notification about a not-connected client {identity}");
            public static void ExistingClientAdded(string identity) => Log.LogWarning((int)EventIds.ExistingClientAdded, $"Received connect notification about a already-connected client {identity}");
            public static void ErrorProcessingNotification(Exception e) => Log.LogError((int)EventIds.ErrorProcessingNotification, e, "Error processing [Connect] notification");
            public static void FailedToObtainConnectionProvider() => Log.LogError((int)EventIds.FailedToObtainConnectionProvider, "Failed to obtain ConnectionProvider");
        }
    }
}
