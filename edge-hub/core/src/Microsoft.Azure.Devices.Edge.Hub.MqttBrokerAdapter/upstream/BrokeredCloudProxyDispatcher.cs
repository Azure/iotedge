// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class BrokeredCloudProxyDispatcher : IMessageConsumer, IMessageProducer
    {
        const string TelemetryTopicTemplate = "$upstream/rpc/pub/{0}/{1}/messages/events/{2}";
        const string EnableTwinDesiredUpdateTemplate = "$upstream/rpc/sub/{0}/{1}/twin/res/"; // this will be interpreted as .../res/#
        const string DisableTwinDesiredUpdateTemplate = "$upstream/rpc/unsub/{0}/{1}/twin/res/";
        const string EnableDirectMethodTemplate = "$upstream/rpc/sub/{0}/{1}/POST/"; // this will be interpreted as .../POST/#
        const string DisableDirectMethodTemplate = "$upstream/rpc/unsub/{0}/{1}/POST/";
        const string GetTwinTemplate = "$upstream/rpc/pub/{0}/{1}/twin/get/?$rid={2}";
        const string UpdateReportedTemplate = "$upstream/rpc/pub/{0}/{1}/twin/reported/?$rid={2}";
        const string DirectMethodResponseTemplate = "$upstream/rpc/pub/{0}/{1}/methods/res/{2}/?$rid={3}";

        const string OpenNotificationTemplate = "$upstream/rpc/pub/{0}/{1}/connection/open";
        const string CloseNotificationTemplate = "$upstream/rpc/pub/{0}/{1}/connection/close";

        const string RpcAckPattern = @"^\$downstream/rpc/ack/(?<cmd>[^/\+\#]+)/(?<guid>[^/\+\#]+)";
        const string TwinGetResponsePattern = @"^\$downstream/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/res/(?<res>.+)/\?\$rid=(?<rid>.+)";
        const string TwinSubscriptionForPatchPattern = @"^\$downstream/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/desired/\?\$version=(?<version>.+)";
        const string MethodCallPattern = @"^\$downstream/(?<id1>[^/\+\#]+)/methods/(?<mname>[^/\+\#]+)/\?\$rid=(?<rid>.+)";

        readonly TimeSpan responseTimeout = TimeSpan.FromSeconds(30); // FIXME make this configurable
        readonly byte[] emptyArray = new byte[0];

        AtomicBoolean isActive = new AtomicBoolean(false);
        long lastRid = 1;

        IEdgeHub edgeHub;
        TaskCompletionSource<IMqttBrokerConnector> connectorGetter = new TaskCompletionSource<IMqttBrokerConnector>();

        ConcurrentDictionary<Guid, TaskCompletionSource<bool>> pendingRpcs = new ConcurrentDictionary<Guid, TaskCompletionSource<bool>>();
        ConcurrentDictionary<long, TaskCompletionSource<IMessage>> pendingTwinRequests = new ConcurrentDictionary<long, TaskCompletionSource<IMessage>>();

        public IReadOnlyCollection<string> Subscriptions => new string[] { "$downstream/#" };

        public void BindEdgeHub(IEdgeHub edgeHub)
        {
            this.edgeHub = edgeHub;
        }

        public bool IsActive => this.isActive;

        public void SetConnector(IMqttBrokerConnector connector) => this.connectorGetter.SetResult(connector);

        public async Task<bool> OpenAsync(IIdentity identity)
        {
            try
            {
                if (!this.isActive.GetAndSet(true))
                {
                    var messageId = Guid.NewGuid();
                    var topic = string.Format(OpenNotificationTemplate, messageId, identity.Id);

                    Events.SendingOpenNotificationForClient(identity.Id);
                    await this.SendUpstreamMessageAsync(messageId, topic, this.emptyArray);
                    Events.SentOpenNotificationForClient(identity.Id);
                }
            }
            catch (Exception e)
            {
                Events.ErrorSendingOpenNotificationForClient(identity.Id, e);
                this.isActive.Set(false);
                throw;
            }

            return true;
        }

        public async Task<bool> CloseAsync(IIdentity identity)
        {
            try
            {
                var messageId = Guid.NewGuid();
                var topic = string.Format(CloseNotificationTemplate, messageId, identity.Id);

                Events.SendingCloseNotificationForClient(identity.Id);
                await this.SendUpstreamMessageAsync(messageId, topic, this.emptyArray);
                Events.SentCloseNotificationForClient(identity.Id);
            }
            catch (Exception e)
            {
                Events.ErrorSendingCloseNotificationForClient(identity.Id, e);
                throw;
            }
            finally
            {
                // still set inactive, even if there was an error
                this.isActive.Set(false);
            }

            return true;
        }

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            try
            {
                var match = Regex.Match(publishInfo.Topic, RpcAckPattern);
                if (match.Success)
                {
                    this.HandleRpcAck(match.Groups["cmd"].Value, match.Groups["guid"].Value);
                    return Task.FromResult(true);
                }

                match = Regex.Match(publishInfo.Topic, TwinGetResponsePattern);
                if (match.Success)
                {
                    this.HandleTwinResponse(this.GetIdFromMatch(match), match.Groups["res"].Value, match.Groups["rid"].Value, publishInfo.Payload);
                    return Task.FromResult(true);
                }

                match = Regex.Match(publishInfo.Topic, TwinSubscriptionForPatchPattern);
                if (match.Success)
                {
                    this.HandleDesiredProperyUpdate(this.GetIdFromMatch(match), match.Groups["version"].Value, publishInfo.Payload);
                    return Task.FromResult(true);
                }

                match = Regex.Match(publishInfo.Topic, MethodCallPattern);
                if (match.Success)
                {
                    this.HandleDirectMethodCall(this.GetIdFromMatch(match), match.Groups["mname"].Value, match.Groups["rid"].Value, publishInfo.Payload);
                    return Task.FromResult(true);
                }
            }
            catch (Exception e)
            {
                Events.ErrorHandlingDownstreamMessage(publishInfo.Topic, e);
            }

            return Task.FromResult(false);
        }

        public async Task SetupCallMethodAsync(IIdentity identity)
        {
            var messageId = Guid.NewGuid();
            var topic = string.Format(EnableDirectMethodTemplate, messageId, identity.Id);

            Events.AddingDirectMethodCallSubscription(identity.Id, messageId);

            await this.SendUpstreamMessageAsync(messageId, topic, this.emptyArray);
        }

        public async Task SetupDesiredPropertyUpdatesAsync(IIdentity identity)
        {
            var messageId = Guid.NewGuid();
            var topic = string.Format(EnableTwinDesiredUpdateTemplate, messageId, identity.Id);

            Events.AddingDesiredPropertyUpdateSubscription(identity.Id, messageId);

            await this.SendUpstreamMessageAsync(messageId, topic, this.emptyArray);
        }

        public async Task RemoveCallMethodAsync(IIdentity identity)
        {
            var messageId = Guid.NewGuid();
            var topic = string.Format(DisableDirectMethodTemplate, messageId, identity.Id);

            Events.RemovingDirectMethodCallSubscription(identity.Id, messageId);

            await this.SendUpstreamMessageAsync(messageId, topic, this.emptyArray);
        }

        public async Task RemoveDesiredPropertyUpdatesAsync(IIdentity identity)
        {
            var messageId = Guid.NewGuid();
            var topic = string.Format(DisableTwinDesiredUpdateTemplate, messageId, identity.Id);

            Events.RemovingDesiredPropertyUpdateSubscription(identity.Id, messageId);

            await this.SendUpstreamMessageAsync(messageId, topic, this.emptyArray);
        }

        public Task SendFeedbackMessageAsync(IIdentity identity, string messageId, FeedbackStatus feedbackStatus)
        {
            // FIXME
            return Task.CompletedTask;
        }

        public async Task SendMessageAsync(IIdentity identity, IMessage message)
        {
            var propertyBag = GetPropertyBag(message);
            var messageId = Guid.NewGuid();
            var topic = string.Format(TelemetryTopicTemplate, messageId, identity.Id, propertyBag);

            Events.SendingTelemetry(identity.Id, messageId);

            await this.SendUpstreamMessageAsync(messageId, topic, message.Body);
        }

        public async Task SendMessageBatchAsync(IIdentity identity, IEnumerable<IMessage> inputMessages)
        {
            foreach (var message in inputMessages)
            {
                await this.SendMessageAsync(identity, message);
            }
        }

        public Task StartListening(IIdentity identity)
        {
            // FIXME not need to do anything, but think it over again
            return Task.CompletedTask;
        }

        public async Task UpdateReportedPropertiesAsync(IIdentity identity, IMessage reportedPropertiesMessage)
        {
            var rid = this.GetRid();
            var taskCompletion = new TaskCompletionSource<IMessage>();

            this.AddPendingRid(rid, identity.Id, taskCompletion);

            var messageId = Guid.NewGuid();
            var topic = string.Format(UpdateReportedTemplate, messageId, identity.Id, rid);

            Events.SendingReportedProperyUpdate(identity.Id, rid);
            await this.SendUpstreamMessageAsync(messageId, topic, reportedPropertiesMessage.Body);
            Events.TwinUpdateSentWaitingResult(identity.Id, rid);

            await this.WaitCompleted(taskCompletion.Task, identity.Id, rid);
            Events.TwinUpdateHasConfirmed(identity.Id, rid);
        }

        public async Task<IMessage> GetTwinAsync(IIdentity identity)
        {
            var rid = this.GetRid();
            var taskCompletion = new TaskCompletionSource<IMessage>();

            this.AddPendingRid(rid, identity.Id, taskCompletion);

            var messageId = Guid.NewGuid();
            var topic = string.Format(GetTwinTemplate, messageId, identity.Id, rid);

            Events.GettingTwin(identity.Id, rid);
            await this.SendUpstreamMessageAsync(messageId, topic, this.emptyArray);
            Events.TwinSentWaitingResult(identity.Id, rid);

            await this.WaitCompleted(taskCompletion.Task, identity.Id, rid);
            Events.TwinResultReceived(identity.Id, rid);

            return taskCompletion.Task.Result;
        }

        void HandleRpcAck(string cmd, string ackedGuid)
        {
            switch (cmd)
            {
                case "pub":
                case "sub":
                case "unsub":
                    {
                        if (!Guid.TryParse(ackedGuid, out var guid))
                        {
                            Events.CannotParseGuid(ackedGuid);
                            return;
                        }

                        if (this.pendingRpcs.TryRemove(guid, out var tsc))
                        {
                            tsc.SetResult(true);
                        }
                        else
                        {
                            Events.CannotFindGuid(ackedGuid);
                        }
                    }

                    break;

                default:
                    Events.UnknownAckType(cmd, ackedGuid);
                    break;
            }
        }

        void HandleTwinResponse(string id, string res, string rid, byte[] payload)
        {
            var messageBuilder = new EdgeMessage.Builder(payload);
            messageBuilder.SetSystemProperties(
                new Dictionary<string, string>()
                {
                    [SystemProperties.StatusCode] = res,
                    [SystemProperties.CorrelationId] = rid,
                });

            var message = messageBuilder.Build();

            long ridAsLong;
            if (!long.TryParse(rid, out ridAsLong)) // FIXME should use as string itself to make less sensitive to the actual format?
            {
                Events.CannotParseRid(rid);
                return;
            }

            if (!this.pendingTwinRequests.TryRemove(ridAsLong, out var tsc))
            {
                Events.CannotFindRid(rid);
                return;
            }

            tsc.SetResult(message);
        }

        void HandleDesiredProperyUpdate(string id, string version, byte[] payload)
        {
            var messageBuilder = new EdgeMessage.Builder(payload);
            messageBuilder.SetSystemProperties(
                new Dictionary<string, string>()
                {
                    [SystemProperties.Version] = version
                });

            _ = this.edgeHub.UpdateDesiredPropertiesAsync(id, messageBuilder.Build());
        }

        void HandleDirectMethodCall(string id, string method, string rid, byte[] payload)
        {
            var callingTask = this.edgeHub.InvokeMethodAsync(rid, new DirectMethodRequest(id, method, payload, TimeSpan.FromMinutes(1))); // FIXME response timeout
            _ = callingTask.ContinueWith(
                    async response =>
                    {
                        var status = response.IsCompletedSuccessfully ? response.Result.Status : 500; // FIXME status in case of error?
                        var messageId = Guid.NewGuid();
                        var topic = string.Format(DirectMethodResponseTemplate, messageId, id, response.Result.Status, rid);

                        await this.SendUpstreamMessageAsync(messageId, topic, response.Result.Data ?? this.emptyArray);
                    });
        }

        string GetIdFromMatch(Match match)
        {
            return match.Groups["id2"].Success
                            ? $"{match.Groups["id1"].Value}/{match.Groups["id2"].Value}"
                            : match.Groups["id1"].Value;
        }

        async Task WaitCompleted(Task task, string id, long rid)
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(this.responseTimeout));
            if (completedTask != task)
            {
                Events.UpstreamMessageTimeout(id, rid);
                throw new TimeoutException("Sending upstream message has timed out");
            }
        }

        void AddPendingRid(long rid, string id, TaskCompletionSource<IMessage> taskCompletion)
        {
            var added = this.pendingTwinRequests.TryAdd(rid, taskCompletion);

            if (!added)
            {
                Events.CannotAddPendingRid(id, rid);
                throw new Exception("Cannot add request id to the pending list, because the id already exists");
            }
        }

        async Task<IMqttBrokerConnector> GetConnector()
        {
            // The following acquires the connection by two steps:
            // 1) It waits till the the connector is set. That variable is set by the connector itself
            //    during initialization, but it is possible that a CouldProxy instance already wants to
            //    communicate upstream (e.g. when edgeHub pulls its twin during startup)
            // 2) It takes time to the connector to actually connect, so the second step waits for
            //    that.
            var connector = await this.connectorGetter.Task;
            await connector.EnsureConnected;

            return connector;
        }

        async Task SendUpstreamMessageAsync(Guid messageId, string topic, byte[] payload)
        {
            var taskCompletion = new TaskCompletionSource<bool>();
            var added = this.pendingRpcs.TryAdd(messageId, taskCompletion);

            if (!added)
            {
                Events.CannotAddPendingRpcId(messageId);
                throw new Exception("Cannot add rpc id to the pending list, because the id already exists");
            }

            Events.SendingUpstreamMessage(messageId);

            var connector = await this.GetConnector();
            await connector.SendAsync(topic, payload);

            Events.SentUpstreamWaitingAck(messageId);

            var completedTask = await Task.WhenAny(taskCompletion.Task, Task.Delay(this.responseTimeout));
            if (completedTask != taskCompletion.Task)
            {
                Events.SentUpstreamTimeout(messageId);
                throw new TimeoutException("Sending upstream message through RPC has timed out.");
            }

            Events.ReceivedConfirmation(messageId);
        }

        long GetRid() => Interlocked.Increment(ref this.lastRid);

        static string GetPropertyBag(IMessage message)
        {
            var properties = new Dictionary<string, string>(message.Properties);

            foreach (KeyValuePair<string, string> systemProperty in message.SystemProperties)
            {
                if (SystemProperties.OutgoingSystemPropertiesMap.TryGetValue(systemProperty.Key, out string onWirePropertyName))
                {
                    properties[onWirePropertyName] = systemProperty.Value;
                }
            }

            return UrlEncodedDictionarySerializer.Serialize(properties);
        }
    }

    static class Events
    {
        const int IdStart = MqttBridgeEventIds.BrokeredCloudProxyDispatcher;
        static readonly ILogger Log = Logger.Factory.CreateLogger<BrokeredCloudProxyDispatcher>();

        enum EventIds
        {
            GettingTwin = IdStart,
            CannotAddPendingRid,
            TwinSentWaitingResult,
            TwinUpdateSentWaitingResult,
            TwinUpdateHasConfirmed,
            UpstreamMessageTimeout,
            TwinResultReceived,

            SendingTelemetry,
            CannotAddPendingRpcId,
            SendingUpstreamMessage,
            SentUpstreamWaitingAck,
            SentUpstreamTimeout,
            ReceivedConfirmation,

            RemovingDirectMethodCallSubscription,
            RemovingDesiredPropertyUpdateSubscription,
            AddingDirectMethodCallSubscription,
            AddingDesiredPropertyUpdateSubscription,

            SendingReportedProperyUpdate,

            ErrorHandlingDownstreamMessage,
            CannotParseGuid,
            CannotFindGuid,
            UnknownAckType,

            SendingOpenNotificationForClient,
            SentOpenNotificationForClient,
            ErrorSendingOpenNotificationForClient,
            SendingCloseNotificationForClient,
            SentCloseNotificationForClient,
            ErrorSendingCloseNotificationForClient
        }

        public static void GettingTwin(string id, long rid) => Log.LogDebug((int)EventIds.GettingTwin, $"Getting twin for client: {id} with request id: {rid}");
        public static void CannotAddPendingRid(string id, long rid) => Log.LogDebug((int)EventIds.CannotAddPendingRid, $"Cannot register request-id when getting twin for client: {id} with request id: {rid}");
        public static void TwinSentWaitingResult(string id, long rid) => Log.LogDebug((int)EventIds.TwinSentWaitingResult, $"Twin request sent for client: {id} with request id: {rid}, waiting for result");
        public static void TwinUpdateSentWaitingResult(string id, long rid) => Log.LogDebug((int)EventIds.TwinUpdateSentWaitingResult, $"Twin update sent from client: {id} with request id: {rid}, waiting for result");
        public static void TwinUpdateHasConfirmed(string id, long rid) => Log.LogDebug((int)EventIds.TwinUpdateSentWaitingResult, $"Twin update has confirmed for client: {id} with request id: {rid}");
        public static void TwinResultReceived(string id, long rid) => Log.LogDebug((int)EventIds.TwinResultReceived, $"Twin received for client: {id} with request id: {rid}");
        public static void CannotParseRid(string rid) => Log.LogError((int)EventIds.CannotParseGuid, "Cannot parse rid: {rid}");
        public static void CannotFindRid(string rid) => Log.LogError((int)EventIds.CannotFindGuid, "Cannot find rid to ACK: {rid}");
        public static void SendingTelemetry(string id, Guid guid) => Log.LogDebug((int)EventIds.SendingTelemetry, $"Sending telemetry message from client: {id} with request id: {guid}");

        public static void UpstreamMessageTimeout(string id, long rid) => Log.LogWarning((int)EventIds.UpstreamMessageTimeout, $"Timeout waiting for result after request sent for client: {id} with request id: {rid}");

        public static void SendingReportedProperyUpdate(string id, long rid) => Log.LogDebug((int)EventIds.GettingTwin, $"Sending reported property update for client: {id} with request id: {rid}");

        public static void CannotAddPendingRpcId(Guid guid) => Log.LogWarning((int)EventIds.CannotAddPendingRpcId, $"Cannot add pending rpc id to the pending list, because it already exists. Id: {guid}");

        public static void SendingUpstreamMessage(Guid guid) => Log.LogDebug((int)EventIds.SendingUpstreamMessage, $"Sending message upstream with id: {guid}");
        public static void SentUpstreamWaitingAck(Guid guid) => Log.LogDebug((int)EventIds.SentUpstreamWaitingAck, $"Sent message upstream, waiting for confirmation. Message id: {guid}");
        public static void SentUpstreamTimeout(Guid guid) => Log.LogWarning((int)EventIds.SentUpstreamTimeout, $"Timeout waiting for upstream message confirmation. Message id: {guid}");
        public static void ReceivedConfirmation(Guid guid) => Log.LogDebug((int)EventIds.ReceivedConfirmation, $"Received confirmation for upstream message. Message id: {guid}");

        public static void AddingDirectMethodCallSubscription(string id, Guid guid) => Log.LogDebug((int)EventIds.AddingDirectMethodCallSubscription, $"Adding direct method call subscritpions for client: {id} with request id: {guid}");
        public static void AddingDesiredPropertyUpdateSubscription(string id, Guid guid) => Log.LogDebug((int)EventIds.AddingDesiredPropertyUpdateSubscription, $"Adding desired property update subscritpions for client: {id} with request id: {guid}");
        public static void RemovingDirectMethodCallSubscription(string id, Guid guid) => Log.LogDebug((int)EventIds.RemovingDirectMethodCallSubscription, $"Removing direct method call subscritpions for client: {id} with request id: {guid}");
        public static void RemovingDesiredPropertyUpdateSubscription(string id, Guid guid) => Log.LogDebug((int)EventIds.RemovingDesiredPropertyUpdateSubscription, $"Removing desired property update subscritpions for client: {id} with request id: {guid}");

        public static void ErrorHandlingDownstreamMessage(string topic, Exception e) => Log.LogError((int)EventIds.ErrorHandlingDownstreamMessage, e, $"Error handling downstream message on topic: {topic}");
        public static void CannotParseGuid(string guid) => Log.LogError((int)EventIds.CannotParseGuid, $"Cannot parse guid: {guid}");
        public static void CannotFindGuid(string guid) => Log.LogError((int)EventIds.CannotFindGuid, $"Cannot find guid to ACK: {guid}");
        public static void UnknownAckType(string cmd, string guid) => Log.LogError((int)EventIds.UnknownAckType, $"Unknown ack type: {cmd} with guid: {guid}");

        public static void SendingOpenNotificationForClient(string id) => Log.LogDebug((int)EventIds.SendingOpenNotificationForClient, $"Sending open notification for client: {id}");
        public static void SentOpenNotificationForClient(string id) => Log.LogDebug((int)EventIds.SentOpenNotificationForClient, $"Sent open notification for client: {id}");
        public static void ErrorSendingOpenNotificationForClient(string id, Exception e) => Log.LogError((int)EventIds.ErrorSendingOpenNotificationForClient, e, $"Error sending open notification for client: {id}");

        public static void SendingCloseNotificationForClient(string id) => Log.LogDebug((int)EventIds.SendingCloseNotificationForClient, $"Sending close notification for client: {id}");
        public static void SentCloseNotificationForClient(string id) => Log.LogDebug((int)EventIds.SentCloseNotificationForClient, $"Sent close notification for client: {id}");
        public static void ErrorSendingCloseNotificationForClient(string id, Exception e) => Log.LogError((int)EventIds.ErrorSendingCloseNotificationForClient, e, $"Error sending close notification for client: {id}");
    }
}
