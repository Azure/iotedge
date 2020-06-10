// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class ConnectionHandler : IConnectionRegistry, ISubscriber, IMessageConsumer
    {
        const string TopicDeviceConnected = "$edgehub/connected";

        static readonly char[] identitySegmentSeparator = new[] { '/' };
        static readonly string[] subscriptions = new[] { TopicDeviceConnected };

        readonly IConnectionProvider connectionProvider;
        readonly DeviceProxy.Factory deviceProxyFactory;

        AsyncLock guard = new AsyncLock();

        // Normal dictionary would be sufficient because of the locks, however we need AddOrUpdate()
        ConcurrentDictionary<IIdentity, IDeviceListener> knownConnections = new ConcurrentDictionary<IIdentity, IDeviceListener>();

        // this class is auto-registered so no way to implement an async activator.
        // hence this one needs to get a Task<T> which is suboptimal, but that is the way
        // IConnectionProvider is registered
        public ConnectionHandler(Task<IConnectionProvider> connectionProvider, DeviceProxy.Factory deviceProxyFactory)
        {
            if (!connectionProvider.IsCompleted)
            {
                // if this leads to a dead-lock, at least it gets logged.
                Events.BlockingDependencyInjection();
            }

            this.connectionProvider = connectionProvider.Result;
            this.deviceProxyFactory = deviceProxyFactory;
        }

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public async Task<Option<IDeviceListener>> GetUpstreamProxyAsync(IIdentity identity)
        {
            using (await this.guard.LockAsync())
            {
                if (this.knownConnections.TryGetValue(identity, out var result))
                {
                    return Option.Some(result);
                }
                else
                {
                    return Option.None<IDeviceListener>();
                }
            }
        }

        public async Task<Option<IDeviceProxy>> GetDownstreamProxyAsync(IIdentity identity)
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

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            switch (publishInfo.Topic)
            {
                case TopicDeviceConnected:
                    // FIXME: a bit worried about that letting it fire and forget,
                    // events can pass each other. this means that in theory it is possible
                    // that an earlier event gets processed later, setting the actual connection
                    // state obsolete.
                    // Maybe the solution would be adding a counter to the package here, as at
                    // this point we are still ordered.
                    _ = this.HandleDeviceConnectedAsync(publishInfo);
                    return Task.FromResult(true);

                default:
                    return Task.FromResult(false);
            }
        }

        async Task HandleDeviceConnectedAsync(MqttPublishInfo mqttPublishInfo)
        {
            var updatedIdentities = this.GetIdentitiesFromUpdateMessage(mqttPublishInfo);
            await updatedIdentities.ForEachAsync(async i => await this.ReconcileConnectionsAsync(new HashSet<IIdentity>(i)));
        }

        async Task ReconcileConnectionsAsync(HashSet<IIdentity> updatedIdentities)
        {
            var identitiesAdded = default(HashSet<IIdentity>);
            var identitiesRemoved = default(HashSet<IIdentity>);

            using (await this.guard.LockAsync())
            {
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
            foreach (var identity in identitiesAdded)
            {
                var deviceListener = await this.connectionProvider.GetDeviceListenerAsync(identity);
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

        Option<List<Identity>> GetIdentitiesFromUpdateMessage(MqttPublishInfo mqttPublishInfo)
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
                return Option.None<List<Identity>>();
            }

            var result = new List<Identity>();

            foreach (var id in identityList)
            {
                var identityComponents = id.Split(identitySegmentSeparator, StringSplitOptions.RemoveEmptyEntries);

                switch (identityComponents.Length)
                {
                    case 1:
                        // FIXME get hubname from somewhere
                        result.Add(new DeviceIdentity("vikauthtest.azure-devices.net", identityComponents[0]));
                        break;

                    case 2:
                        result.Add(new ModuleIdentity("vikauthtest.azure-devices.net", identityComponents[0], identityComponents[1]));
                        break;

                    default:
                        Events.BadIdentityFormat(id);
                        continue;
                }
            }

            return Option.Some(result);
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
                BlockingDependencyInjection
            }

            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize connection update");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void UnknownClientDisconnected(string identity) => Log.LogWarning((int)EventIds.UnknownClientDisconnected, $"Received disconnect notification about a not-connected client {identity}");
            public static void ExistingClientAdded(string identity) => Log.LogWarning((int)EventIds.ExistingClientAdded, $"Received connect notification about a already-connected client {identity}");
            public static void BlockingDependencyInjection() => Log.LogWarning((int)EventIds.BlockingDependencyInjection, $"Blocking dependency injection as IConnectionProvider is not available at the time");
        }
    }
}
