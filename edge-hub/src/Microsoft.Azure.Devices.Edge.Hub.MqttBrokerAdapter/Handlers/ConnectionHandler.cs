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

    public class ConnectionHandler : IConnectionRegistry, ISubscriber, IMessageConsumer
    {
        const string TopicDeviceConnected = "$edgehub/connected";

        static readonly string[] subscriptions = new[] { TopicDeviceConnected };

        readonly IConnectionProvider connectionProvider;
        readonly IIdentityProvider identityProvider;
        readonly ISystemComponentIdProvider systemComponentIdProvider;
        readonly DeviceProxy.Factory deviceProxyFactory;

        readonly Channel<MqttPublishInfo> notifications;
        readonly Task processingLoop;

        AsyncLock guard = new AsyncLock();

        // Normal dictionary would be sufficient because of the locks, however we need AddOrUpdate()
        ConcurrentDictionary<IIdentity, IDeviceListener> knownConnections = new ConcurrentDictionary<IIdentity, IDeviceListener>();

        // this class is auto-registered so no way to implement an async activator.
        // hence this one needs to get a Task<T> which is suboptimal, but that is the way
        // IConnectionProvider is registered
        public ConnectionHandler(Task<IConnectionProvider> connectionProvider, IIdentityProvider identityProvider, ISystemComponentIdProvider systemComponentIdProvider, DeviceProxy.Factory deviceProxyFactory)
        {
            if (!connectionProvider.IsCompleted)
            {
                // if this leads to a dead-lock, at least it gets logged.
                Events.BlockingDependencyInjection();
            }

            this.connectionProvider = connectionProvider.Result;
            this.identityProvider = identityProvider;
            this.systemComponentIdProvider = systemComponentIdProvider;
            this.deviceProxyFactory = deviceProxyFactory;

            this.notifications = Channel.CreateUnbounded<MqttPublishInfo>(
                                    new UnboundedChannelOptions
                                    {
                                        SingleReader = true,
                                        SingleWriter = true
                                    });

            this.processingLoop = this.StartProcessingLoop();
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
                    if (!this.notifications.Writer.TryWrite(publishInfo))
                    {
                        Events.ErrorEnqueueingNotification();
                    }

                    return Task.FromResult(true);

                default:
                    return Task.FromResult(false);
            }
        }

        public void ProducerStopped()
        {
            this.notifications.Writer.Complete();
            this.processingLoop.Wait();
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

                var identityComponents = id.Split(HandlerUtils.IdentitySegmentSeparator, StringSplitOptions.RemoveEmptyEntries);

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

        bool IsSystemComponent(string id)
        {
            return string.Equals(id, systemComponentIdProvider.EdgeHubBridgeId);
        }

        Task StartProcessingLoop()
        {
            var loopTask = Task.Run(
                                async () =>
                                {
                                    Events.ProcessingLoopStarted();

                                    while (await this.notifications.Reader.WaitToReadAsync())
                                    {
                                        var publishInfo = await this.notifications.Reader.ReadAsync();

                                        try
                                        {
                                            await this.HandleDeviceConnectedAsync(publishInfo);
                                        }
                                        catch (Exception e)
                                        {
                                            Events.ErrorProcessingNotification(e);
                                        }
                                    }

                                    Events.ProcessingLoopStopped();
                                });

            return loopTask;
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
                ProcessingLoopStarted,
                ProcessingLoopStopped,
                ErrorEnqueueingNotification,
                ErrorProcessingNotification
            }

            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize connection update");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void UnknownClientDisconnected(string identity) => Log.LogWarning((int)EventIds.UnknownClientDisconnected, $"Received disconnect notification about a not-connected client {identity}");
            public static void ExistingClientAdded(string identity) => Log.LogWarning((int)EventIds.ExistingClientAdded, $"Received connect notification about a already-connected client {identity}");
            public static void BlockingDependencyInjection() => Log.LogWarning((int)EventIds.BlockingDependencyInjection, $"Blocking dependency injection as IConnectionProvider is not available at the time");
            public static void ProcessingLoopStarted() => Log.LogInformation((int)EventIds.ProcessingLoopStarted, "Processing loop started");
            public static void ProcessingLoopStopped() => Log.LogInformation((int)EventIds.ProcessingLoopStopped, "Processing loop stopped");
            public static void ErrorEnqueueingNotification() => Log.LogError((int)EventIds.ErrorEnqueueingNotification, "Error enqueueing notification");
            public static void ErrorProcessingNotification(Exception e) => Log.LogError((int)EventIds.ErrorProcessingNotification, e, "Error processing [Connect] notification");
        }
    }
}
