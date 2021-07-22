// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class ConnectionHandler : IConnectionRegistry, IMessageConsumer, IMessageProducer
    {
        const string TopicDeviceConnected = "$edgehub/connected";
        const string TopicDisconnect = "$edgehub/disconnect";

        static readonly char[] identitySegmentSeparator = new[] { '/' };
        static readonly string[] subscriptions = new[] { TopicDeviceConnected };

        readonly Task<IConnectionProvider> connectionProviderGetter;
        readonly Task<IAuthenticator> authenticatorGetter;
        readonly IIdentityProvider identityProvider;
        readonly ISystemComponentIdProvider systemComponentIdProvider;
        readonly DeviceProxy.Factory deviceProxyFactory;

        IMqttBrokerConnector connector;

        AsyncLock guard = new AsyncLock();

        // Normal dictionary would be sufficient because of the locks, however we need AddOrUpdate()
        ConcurrentDictionary<IIdentity, IDeviceListener> knownConnections = new ConcurrentDictionary<IIdentity, IDeviceListener>();

        // this class is auto-registered so no way to implement an async activator.
        // hence this one needs to get a Task<T> which is suboptimal, but that is the way
        // IConnectionProvider is registered
        public ConnectionHandler(Task<IConnectionProvider> connectionProviderGetter, Task<IAuthenticator> authenticatorGetter, IIdentityProvider identityProvider, ISystemComponentIdProvider systemComponentIdProvider, DeviceProxy.Factory deviceProxyFactory)
        {
            this.connectionProviderGetter = Preconditions.CheckNotNull(connectionProviderGetter);
            this.authenticatorGetter = Preconditions.CheckNotNull(authenticatorGetter);
            this.identityProvider = Preconditions.CheckNotNull(identityProvider);
            this.systemComponentIdProvider = Preconditions.CheckNotNull(systemComponentIdProvider);
            this.deviceProxyFactory = Preconditions.CheckNotNull(deviceProxyFactory);
        }

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public void SetConnector(IMqttBrokerConnector connector) => this.connector = connector;

        public async Task<Option<IDeviceListener>> GetOrCreateDeviceListenerAsync(IIdentity identity, bool directOnCreation)
        {
            using (await this.guard.LockAsync())
            {
                if (this.knownConnections.TryGetValue(identity, out IDeviceListener result))
                {
                    return Option.Some(result);
                }
                else
                {
                    return await this.CreateDeviceListenerAsync(identity, directOnCreation);
                }
            }
        }

        public async Task<Option<IDeviceProxy>> GetDeviceProxyAsync(IIdentity identity)
        {
            using (await this.guard.LockAsync())
            {
                var container = default(IDeviceListener);
                if (!this.knownConnections.TryGetValue(identity, out container))
                {
                    return await this.CreateDeviceProxyAsync(identity);
                }

                return container switch
                                 {
                                    IDeviceProxy proxy => Option.Some(proxy),
                                    _ => Option.None<IDeviceProxy>()
                                 };
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

        public async Task CloseConnectionAsync(IIdentity identity)
        {
            var identityFound = false;
            using (await this.guard.LockAsync())
            {
                identityFound = this.knownConnections.TryGetValue(identity, out var _);
            }

            if (identityFound)
            {
                await this.connector.SendAsync(TopicDisconnect, Encoding.UTF8.GetBytes($"\"{identity.Id}\""));
            }
            else
            {
                Events.CouldNotFindClientToClose(identity.Id);
            }
        }

        public async Task<IReadOnlyList<IIdentity>> GetNestedConnectionsAsync()
        {
            using (await this.guard.LockAsync())
            {
                return this.knownConnections.Values.OfType<IDeviceProxy>().Where(p => !p.IsDirectClient).Select(p => p.Identity).ToArray();
            }
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

                // When a device connects indirectly, then disconnects and connects directly again, the following happens:
                // As we don't have "indirect disconnection", the device proxy instance lingers for a while before gets deleted
                // by idle-time. Because of that it may show up in the "knownIdentities" list, and the way added connections are
                // calculated above, it will not be added as a "direct connection" but keeps existing as "indirect connection".
                // As a result, the device proxy will use mqtt topics for indirect connections which will not be subscribed
                // by the directly connected device. As a solution, check if the broker sent connection-list and the known
                // connections match by 'IsDirect' flag. If not, remove the indirect connection and recreate them as direct.
                // Note, that a (child) $edgeHub is a direct connection but uses indirect dialect, so it has the IsDirect flag
                // turned off, so ignore those cases from the list.
                var reconnectedCandidates = new HashSet<IIdentity>(updatedIdentities);
                var indirectConnections = this.knownConnections.Values.OfType<IDeviceProxy>()
                                                                      .Where(p => !p.IsDirectClient && !(p.Identity is ModuleIdentity i && string.Equals(i.ModuleId, Constants.EdgeHubModuleId)))
                                                                      .Select(p => p.Identity);
                reconnectedCandidates.IntersectWith(indirectConnections);
                await this.ReplaceConnectionsAsync(reconnectedCandidates);
            }
        }

        async Task RemoveConnectionsAsync(HashSet<IIdentity> identitiesRemoved)
        {
            foreach (var identity in identitiesRemoved)
            {
                if (this.knownConnections.TryGetValue(identity, out IDeviceListener container))
                {
                    if (container is IDeviceProxy proxy)
                    {
                        // Clients connected indirectly (through a child edge device) will not be reported
                        // by broker events and appear in the 'identitiesRemoved' list as missing identities.
                        // Ignore those:
                        if (!proxy.IsDirectClient)
                        {
                            continue;
                        }
                    }
                }

                if (this.knownConnections.TryRemove(identity, out var deviceListener))
                {
                    await deviceListener.RemoveSubscriptions();
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
                await this.AddConnectionAsync(identity, true, connectionProvider);
            }
        }

        async Task ReplaceConnectionsAsync(HashSet<IIdentity> identitiesReplaced)
        {
            var connectionProvider = await this.connectionProviderGetter;
            if (connectionProvider == null)
            {
                Events.FailedToObtainConnectionProvider();
                return;
            }

            foreach (var identity in identitiesReplaced)
            {
                await this.AddConnectionAsync(identity, true, connectionProvider);
            }
        }

        async Task<IDeviceListener> AddConnectionAsync(IIdentity identity, bool isDirectConnection, IConnectionProvider connectionProvider)
        {
            var deviceListener = await connectionProvider.GetDeviceListenerAsync(identity, Option.None<string>());
            var deviceProxy = this.deviceProxyFactory(identity, isDirectConnection);

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

            try
            {
                if (previousListener != null)
                {
                    Events.ExistingClientAdded(previousListener.Identity.Id);
                    await previousListener.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                Events.CouldNotClosePreviousClient(identity.Id, ex);
                // keep going
            }

            return deviceListener;
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
                if (this.systemComponentIdProvider.IsSystemComponent(id))
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

        async Task<Option<IDeviceListener>> CreateDeviceListenerAsync(IIdentity identity, bool directOnCreation)
        {
            var connectionProvider = await this.connectionProviderGetter;
            if (connectionProvider == null)
            {
                Events.FailedToObtainConnectionProvider();
                return Option.None<IDeviceListener>();
            }

            if (!directOnCreation)
            {
                var clientCredentials = new ImplicitCredentials(identity, string.Empty, Option.None<string>()); // TODO obtain prod info/model id
                var authenticator = await this.authenticatorGetter;
                await authenticator.AuthenticateAsync(clientCredentials);
            }

            var deviceListener = await this.AddConnectionAsync(identity, directOnCreation, connectionProvider);
            return Option.Some(deviceListener);
        }

        async Task<Option<IDeviceProxy>> CreateDeviceProxyAsync(IIdentity identity)
        {
            var deviceListener = await this.CreateDeviceListenerAsync(identity, false);
            return deviceListener.FlatMap(
                        listener =>
                            listener switch
                            {
                                IDeviceProxy proxy => Option.Some(proxy),
                                _ => Option.None<IDeviceProxy>()
                            });
        }

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
                FailedToObtainConnectionProvider,
                CouldNotFindClientToClose,
                CouldNotClosePreviousClient
            }

            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize connection update");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void UnknownClientDisconnected(string identity) => Log.LogWarning((int)EventIds.UnknownClientDisconnected, $"Received disconnect notification about a not-connected client {identity}");
            public static void ExistingClientAdded(string identity) => Log.LogInformation((int)EventIds.ExistingClientAdded, $"Received connect notification about a already-connected client {identity}");
            public static void ErrorProcessingNotification(Exception e) => Log.LogError((int)EventIds.ErrorProcessingNotification, e, "Error processing [Connect] notification");
            public static void FailedToObtainConnectionProvider() => Log.LogError((int)EventIds.FailedToObtainConnectionProvider, "Failed to obtain ConnectionProvider");
            public static void CouldNotFindClientToClose(string identity) => Log.LogInformation((int)EventIds.CouldNotFindClientToClose, $"Could not find to close: {identity}. No signal will be sent to the broker");
            public static void CouldNotClosePreviousClient(string identity, Exception e) => Log.LogError((int)EventIds.CouldNotClosePreviousClient, e, $"Could not close previous device connection for: {identity}");
        }
    }
}
