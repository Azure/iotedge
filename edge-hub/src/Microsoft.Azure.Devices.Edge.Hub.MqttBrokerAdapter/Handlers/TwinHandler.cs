// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class TwinHandler : ITwinHandler, IMessageConsumer, IMessageProducer
    {
        const string TwinGetDevice = "$edgehub/+/twin/get/#";
        const string TwinGetModule = "$edgehub/+/+/twin/get/#";
        const string TwinUpdateDevice = "$edgehub/+/twin/reported/#";
        const string TwinUpdateModule = "$edgehub/+/+/twin/reported/#";

        const string TwinGetPublishPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/get/\?\$rid=(?<rid>.+)";
        const string TwinUpdatePublishPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/reported/\?\$rid=(?<rid>.+)";

        const string TwinSubscriptionForResultsPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/res/\#$";
        const string TwinSubscriptionForPatchPattern = @"^\$edgehub/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/desired/\#$";

        const string TwinResultDevice = "$edgehub/{0}/twin/res/{1}/?$rid={2}";
        const string TwinResultModule = "$edgehub/{0}/{1}/twin/res/{2}/?$rid={3}";

        const string DesiredUpdateDevice = "$edgehub/{0}/twin/desired/?$version={1}";
        const string DesiredUpdateModule = "$edgehub/{0}/{1}/twin/desired/?$version={2}";

        static readonly string[] subscriptions = new[] { TwinGetDevice, TwinGetModule, TwinUpdateDevice, TwinUpdateModule };

        static readonly SubscriptionPattern[] subscriptionPatterns = new SubscriptionPattern[]
                                                                         {
                                                                            new SubscriptionPattern(TwinSubscriptionForResultsPattern, DeviceSubscription.TwinResponse),
                                                                            new SubscriptionPattern(TwinSubscriptionForPatchPattern, DeviceSubscription.DesiredPropertyUpdates)
                                                                         };
        readonly Channel<ProcessingInfo> notifications;
        readonly Task processingLoop;

        readonly IConnectionRegistry connectionRegistry;
        readonly IIdentityProvider identityProvider;

        IMqttBrokerConnector connector;

        public IReadOnlyCollection<string> Subscriptions => subscriptions;

        public TwinHandler(IConnectionRegistry connectionRegistry, IIdentityProvider identityProvider)
        {
            this.connectionRegistry = Preconditions.CheckNotNull(connectionRegistry);
            this.identityProvider = Preconditions.CheckNotNull(identityProvider);

            this.notifications = Channel.CreateUnbounded<ProcessingInfo>(
                                    new UnboundedChannelOptions
                                    {
                                        SingleReader = true,
                                        SingleWriter = true
                                    });

            this.processingLoop = this.StartProcessingLoop();
        }

        public IReadOnlyCollection<SubscriptionPattern> WatchedSubscriptions => subscriptionPatterns;

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            var isEnqueued = default(bool);
            var match = Regex.Match(publishInfo.Topic, TwinGetPublishPattern);
            if (match.Success)
            {
                isEnqueued = this.notifications.Writer.TryWrite(new ProcessingInfo(Direction.Get, match, publishInfo));
                if (!isEnqueued)
                {
                    Events.ErrorEnqueueingNotification();
                }
            }

            match = Regex.Match(publishInfo.Topic, TwinUpdatePublishPattern);
            if (match.Success)
            {
                isEnqueued = this.notifications.Writer.TryWrite(new ProcessingInfo(Direction.Set, match, publishInfo));
                if (!isEnqueued)
                {
                    Events.ErrorEnqueueingNotification();
                }
            }

            return Task.FromResult(isEnqueued);
        }

        public void ProducerStopped()
        {
            this.notifications.Writer.Complete();
            this.processingLoop.Wait();
        }

        public void SetConnector(IMqttBrokerConnector connector) => this.connector = connector;

        public async Task SendTwinUpdate(IMessage twin, IIdentity identity)
        {
            var statusCode = string.Empty;
            var correlationId = string.Empty;

            var allPropertiesPresent = true;

            allPropertiesPresent = allPropertiesPresent && twin.SystemProperties.TryGetValue(SystemProperties.StatusCode, out statusCode);
            allPropertiesPresent = allPropertiesPresent && twin.SystemProperties.TryGetValue(SystemProperties.CorrelationId, out correlationId);

            if (allPropertiesPresent)
            {
                bool result;
                try
                {
                    result = await this.connector.SendAsync(
                                                    GetTwinResultTopic(identity, statusCode, correlationId),
                                                    twin.Body);
                }
                catch (Exception e)
                {
                    Events.FailedToSendTwinUpdateMessage(e);
                    result = false;
                }

                if (result)
                {
                    Events.TwinUpdate(identity.Id, statusCode, correlationId, twin.Body.Length);
                }
                else
                {
                    Events.TwinUpdateFailed(identity.Id, statusCode, correlationId, twin.Body.Length);
                }
            }
            else
            {
                Events.TwinUpdateIncompete(identity.Id);
            }
        }

        public async Task SendDesiredPropertiesUpdate(IMessage desiredProperties, IIdentity identity)
        {
            if (!desiredProperties.SystemProperties.TryGetValue(SystemProperties.Version, out var version))
            {
                Events.DesiredPropertiesUpdateIncompete(identity.Id);
                return;
            }

            bool result;
            try
            {
                result = await this.connector.SendAsync(
                                                GetDesiredPropertiesUpdateTopic(identity, version),
                                                desiredProperties.Body);
            }
            catch (Exception e)
            {
                Events.FailedToSendDesiredPropertiesUpdateMessage(e);
                result = false;
            }

            if (result)
            {
                Events.DesiredPropertiesUpdate(identity.Id, version, desiredProperties.Body.Length);
            }
            else
            {
                Events.DesiredPropertiesUpdateFailed(identity.Id, version, desiredProperties.Body.Length);
            }
        }

        Task<bool> HandleTwinGet(Match match, MqttPublishInfo publishInfo)
        {
            return this.HandleUpstreamRequest(
                        async (proxy, rid) =>
                        {
                            await proxy.SendGetTwinRequest(rid);
                        },
                        match,
                        publishInfo);
        }

        Task<bool> HandleUpdateReported(Match match, MqttPublishInfo publishInfo)
        {
            return this.HandleUpstreamRequest(
                        async (proxy, rid) =>
                        {
                            var message = new EdgeMessage.Builder(publishInfo.Payload).Build();
                            await proxy.UpdateReportedPropertiesAsync(message, rid);
                        },
                        match,
                        publishInfo);
        }

        async Task<bool> HandleUpstreamRequest(Func<IDeviceListener, string, Task> action, Match match, MqttPublishInfo publishInfo)
        {
            var id1 = match.Groups["id1"];
            var id2 = match.Groups["id2"];
            var rid = match.Groups["rid"];

            var identity = id2.Success
                                ? this.identityProvider.Create(id1.Value, id2.Value)
                                : this.identityProvider.Create(id1.Value);

            var maybeProxy = await this.connectionRegistry.GetDeviceListenerAsync(identity);
            var proxy = default(IDeviceListener);

            try
            {
                proxy = maybeProxy.Expect(() => new Exception($"No device listener found for {identity.Id}"));
            }
            catch (Exception)
            {
                Events.MissingProxy(identity.Id);
                return false;
            }

            var message = new EdgeMessage.Builder(publishInfo.Payload).Build();
            await action(proxy, rid.Value);

            return true;
        }

        Task StartProcessingLoop()
        {
            var loopTask = Task.Run(
                                async () =>
                                {
                                    Events.ProcessingLoopStarted();

                                    while (await this.notifications.Reader.WaitToReadAsync())
                                    {
                                        var processingInfo = await this.notifications.Reader.ReadAsync();

                                        try
                                        {
                                            if (processingInfo.Direction == Direction.Get)
                                            {
                                                await this.HandleTwinGet(processingInfo.Match, processingInfo.PublishInfo);
                                            }
                                            else
                                            {
                                                await this.HandleUpdateReported(processingInfo.Match, processingInfo.PublishInfo);
                                            }
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

        static string GetTwinResultTopic(IIdentity identity, string statusCode, string correlationId)
        {
            switch (identity)
            {
                case IModuleIdentity moduleIdentity:
                    return string.Format(TwinResultModule, moduleIdentity.DeviceId, moduleIdentity.ModuleId, statusCode, correlationId);

                case IDeviceIdentity deviceIdentity:
                    return string.Format(TwinResultDevice, deviceIdentity.DeviceId, statusCode, correlationId);

                default:
                    Events.BadIdentityFormat(identity.Id);
                    throw new Exception($"cannot decode identity {identity.Id}");
            }
        }

        static string GetDesiredPropertiesUpdateTopic(IIdentity identity, string version)
        {
            switch (identity)
            {
                case IModuleIdentity moduleIdentity:
                    return string.Format(DesiredUpdateModule, moduleIdentity.DeviceId, moduleIdentity.ModuleId, version);

                case IDeviceIdentity deviceIdentity:
                    return string.Format(DesiredUpdateDevice, deviceIdentity.DeviceId, version);

                default:
                    Events.BadIdentityFormat(identity.Id);
                    throw new Exception($"cannot decode identity {identity.Id}");
            }
        }

        static class Events
        {
            const int IdStart = MqttBridgeEventIds.TwinHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<TwinHandler>();

            enum EventIds
            {
                TwinUpdate = IdStart,
                TwinUpdateFailed,
                TwinUpdateIncompete,
                DesiredPropertiesUpdate,
                DesiredPropertiesUpdateFailed,
                DesiredPropertiesUpdateIncompete,
                MissingProxy,
                UnexpectedTwinTopic,
                BadIdentityFormat,
                FailedToSendTwinUpdateMessage,
                FailedToSendDesiredPropertiesUpdateMessage,
                BadPayloadFormat,
                ProcessingLoopStarted,
                ProcessingLoopStopped,
                ErrorEnqueueingNotification,
                ErrorProcessingNotification
            }

            public static void TwinUpdate(string id, string statusCode, string correlationId, int messageLen) => Log.LogDebug((int)EventIds.TwinUpdate, $"Twin Update sent to client: {id}, status: {statusCode}, rid: {correlationId}, msg len: {messageLen}");
            public static void TwinUpdateFailed(string id, string statusCode, string correlationId, int messageLen) => Log.LogError((int)EventIds.TwinUpdateFailed, $"Failed to send Twin Update to client: {id}, status: {statusCode}, rid: {correlationId}, msg len: {messageLen}");
            public static void DesiredPropertiesUpdate(string id, string version, int messageLen) => Log.LogDebug((int)EventIds.DesiredPropertiesUpdate, $"Desired Properties Update sent to client: {id}, version: {version}, msg len: {messageLen}");
            public static void DesiredPropertiesUpdateFailed(string id, string version, int messageLen) => Log.LogError((int)EventIds.DesiredPropertiesUpdateFailed, $"Failed to send Desired Properties Update to client: {id}, status: {version}, msg len: {messageLen}");
            public static void TwinUpdateIncompete(string id) => Log.LogError((int)EventIds.TwinUpdateIncompete, $"Failed to send Twin Update to client {id} because the message is incomplete - not all system properties are present");
            public static void DesiredPropertiesUpdateIncompete(string id) => Log.LogError((int)EventIds.DesiredPropertiesUpdateIncompete, $"Failed to send Desired Properties Update to client {id} because the message is incomplete - not all system properties are present");
            public static void MissingProxy(string id) => Log.LogError((int)EventIds.MissingProxy, $"Missing device listener for {id}");
            public static void UnexpectedTwinTopic(string topic) => Log.LogWarning((int)EventIds.UnexpectedTwinTopic, $"Twin-like topic strucure with unexpected format {topic}");
            public static void BadIdentityFormat(string identity) => Log.LogError((int)EventIds.BadIdentityFormat, $"Bad identity format: {identity}");
            public static void FailedToSendTwinUpdateMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendTwinUpdateMessage, e, "Failed to send twin update message");
            public static void FailedToSendDesiredPropertiesUpdateMessage(Exception e) => Log.LogError((int)EventIds.FailedToSendDesiredPropertiesUpdateMessage, e, "Failed to send Desired Properties Update message");
            public static void BadPayloadFormat(Exception e) => Log.LogError((int)EventIds.BadPayloadFormat, e, "Bad payload format: cannot deserialize subscription update");
            public static void ProcessingLoopStarted() => Log.LogInformation((int)EventIds.ProcessingLoopStarted, "Processing loop started");
            public static void ProcessingLoopStopped() => Log.LogInformation((int)EventIds.ProcessingLoopStopped, "Processing loop stopped");
            public static void ErrorEnqueueingNotification() => Log.LogError((int)EventIds.ErrorEnqueueingNotification, "Error enqueueing notification");
            public static void ErrorProcessingNotification(Exception e) => Log.LogError((int)EventIds.ErrorProcessingNotification, e, "Error processing Twin notification");
        }

        public enum Direction
        {
            Get,
            Set
        }

        class ProcessingInfo
        {
            public ProcessingInfo(Direction direction, Match match, MqttPublishInfo publishInfo)
            {
                this.Direction = direction;
                this.Match = match;
                this.PublishInfo = publishInfo;
            }

            public Direction Direction { get; }
            public Match Match { get; }
            public MqttPublishInfo PublishInfo { get; }
        }
    }
}
