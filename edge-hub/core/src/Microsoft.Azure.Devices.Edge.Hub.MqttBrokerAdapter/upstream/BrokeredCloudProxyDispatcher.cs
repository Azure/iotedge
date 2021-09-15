// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Bson;
    using Newtonsoft.Json.Serialization;

    public class BrokeredCloudProxyDispatcher : IMessageConsumer, IMessageProducer
    {
        const string TelemetryTopicTemplate = "$iothub/{0}/messages/events/{1}";
        const string TwinDesiredUpdateSubscriptionTemplate = "$iothub/{0}/twin/desired/#";
        const string TwinResultSubscriptionTemplate = "$iothub/{0}/twin/res/#";
        const string DirectMethodSubscriptionTemplate = "$iothub/{0}/methods/post/#";
        const string GetTwinTemplate = "$iothub/{0}/twin/get/?$rid={1}";
        const string UpdateReportedTemplate = "$iothub/{0}/twin/reported/?$rid={1}";
        const string DirectMethodResponseTemplate = "$iothub/{0}/methods/res/{1}/?$rid={2}";
        const string RpcTopicTemplate = "$upstream/rpc/{0}";

        const string RpcVersion = "v1";
        const string RpcCmdSub = "sub";
        const string RpcCmdUnsub = "unsub";
        const string RpcCmdPub = "pub";

        const string RpcAckPattern = @"^\$downstream/rpc/ack/(?<guid>[^/\+\#]+)";
        const string TwinGetResponsePattern = @"^\$downstream/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/res/(?<res>.+)/\?\$rid=(?<rid>.+)";
        const string TwinSubscriptionForPatchPattern = @"^\$downstream/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/twin/desired/\?\$version=(?<version>.+)";
        const string MethodCallPattern = @"^\$downstream/(?<id1>[^/\+\#]+)(/(?<id2>[^/\+\#]+))?/methods/post/(?<mname>[^/\+\#]+)/\?\$rid=(?<rid>.+)";

        const string DownstreamTopic = "$downstream/#";
        const string ConnectivityTopic = "$internal/connectivity";

        readonly TimeSpan responseTimeout = TimeSpan.FromSeconds(30); // TODO should come from configuration
        readonly byte[] emptyArray = new byte[0];

        long lastRid = 1;
        AtomicBoolean isConnected = new AtomicBoolean(false);

        IEdgeHub edgeHub;
        TaskCompletionSource<IMqttBrokerConnector> connectorGetter = new TaskCompletionSource<IMqttBrokerConnector>();

        ConcurrentDictionary<Guid, TaskCompletionSource<bool>> pendingRpcs = new ConcurrentDictionary<Guid, TaskCompletionSource<bool>>();
        ConcurrentDictionary<long, TaskCompletionSource<IMessage>> pendingTwinRequests = new ConcurrentDictionary<long, TaskCompletionSource<IMessage>>();

        public event Action<CloudConnectionStatus> ConnectionStatusChangedEvent;

        public IReadOnlyCollection<string> Subscriptions => new string[] { DownstreamTopic, ConnectivityTopic };

        public bool IsConnected => this.isConnected.Get();

        public void BindEdgeHub(IEdgeHub edgeHub)
        {
            this.edgeHub = edgeHub;
        }

        public void SetConnector(IMqttBrokerConnector connector) => this.connectorGetter.SetResult(connector);

        public Task<bool> HandleAsync(MqttPublishInfo publishInfo)
        {
            try
            {
                var match = Regex.Match(publishInfo.Topic, RpcAckPattern);
                if (match.Success)
                {
                    this.HandleRpcAck(match.Groups["guid"].Value);
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

                if (ConnectivityTopic.Equals(publishInfo.Topic))
                {
                    this.HandleConnectivityEvent(publishInfo.Payload);
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
            Events.AddingDirectMethodCallSubscription(identity.Id);

            var topic = string.Format(DirectMethodSubscriptionTemplate, identity.Id);
            await this.SendUpstreamMessageAsync(RpcCmdSub, topic, this.emptyArray);
        }

        public async Task SetupDesiredPropertyUpdatesAsync(IIdentity identity)
        {
            Events.AddingDesiredPropertyUpdateSubscription(identity.Id);

            var topic = string.Format(TwinDesiredUpdateSubscriptionTemplate, identity.Id);
            await this.SendUpstreamMessageAsync(RpcCmdSub, topic, this.emptyArray);
        }

        public async Task RemoveCallMethodAsync(IIdentity identity)
        {
            Events.RemovingDirectMethodCallSubscription(identity.Id);

            var topic = string.Format(DirectMethodSubscriptionTemplate, identity.Id);
            await this.SendUpstreamMessageAsync(RpcCmdUnsub, topic, this.emptyArray);
        }

        public async Task RemoveDesiredPropertyUpdatesAsync(IIdentity identity)
        {
            Events.RemovingDesiredPropertyUpdateSubscription(identity.Id);

            var topic = string.Format(TwinDesiredUpdateSubscriptionTemplate, identity.Id);
            await this.SendUpstreamMessageAsync(RpcCmdUnsub, topic, this.emptyArray);
        }

        public async Task RemoveTwinResponseAsync(IIdentity identity)
        {
            Events.RemovingTwinResultSubscription(identity.Id);

            var topic = string.Format(TwinResultSubscriptionTemplate, identity.Id);
            await this.SendUpstreamMessageAsync(RpcCmdUnsub, topic, this.emptyArray);
        }

        public Task SendFeedbackMessageAsync(IIdentity identity, string messageId, FeedbackStatus feedbackStatus)
        {
            // TODO: when M2M/C2D is implemented, this may need to do something
            return Task.CompletedTask;
        }

        public async Task SendMessageAsync(IIdentity identity, IMessage message)
        {
            Events.SendingTelemetry(identity.Id);

            var propertyBag = GetPropertyBag(message);
            var topic = string.Format(TelemetryTopicTemplate, identity.Id, propertyBag);

            await this.SendUpstreamMessageAsync(RpcCmdPub, topic, message.Body);
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
            // No need to listen as the notifications will be pushed by the normal event handling mechanism
            return Task.CompletedTask;
        }

        public Task StopListening(IIdentity identity)
        {
            // No listener
            return Task.CompletedTask;
        }

        public async Task UpdateReportedPropertiesAsync(IIdentity identity, IMessage reportedPropertiesMessage, bool needSubscribe)
        {
            if (needSubscribe)
            {
                // TODO c# sdk subscribes automatically when needed, so do we. Needs to reconsider if this is the good place
                // and how to handle the related state (the needSubscribe flag)
                await this.SendUpstreamMessageAsync(RpcCmdSub, string.Format(TwinResultSubscriptionTemplate, identity.Id), this.emptyArray);
            }

            var rid = this.GetRid();
            var taskCompletion = new TaskCompletionSource<IMessage>();

            this.AddPendingRid(rid, identity.Id, taskCompletion);

            var topic = string.Format(UpdateReportedTemplate, identity.Id, rid);

            Events.SendingReportedProperyUpdate(identity.Id, rid);
            await this.SendUpstreamMessageAsync(RpcCmdPub, topic, reportedPropertiesMessage.Body);
            Events.TwinUpdateSentWaitingResult(identity.Id, rid);

            await this.WaitCompleted(taskCompletion.Task, identity.Id, rid);
            Events.TwinUpdateHasConfirmed(identity.Id, rid);
        }

        public async Task<IMessage> GetTwinAsync(IIdentity identity, bool needSubscribe)
        {
            if (needSubscribe)
            {
                // TODO c# sdk subscribes automatically when needed, so do we. Needs to reconsider if this is the good place
                // and how to handle the related state (the needSubscribe flag)
                await this.SendUpstreamMessageAsync(RpcCmdSub, string.Format(TwinResultSubscriptionTemplate, identity.Id), this.emptyArray);
            }

            var rid = this.GetRid();
            var taskCompletion = new TaskCompletionSource<IMessage>();

            this.AddPendingRid(rid, identity.Id, taskCompletion);

            var topic = string.Format(GetTwinTemplate, identity.Id, rid);

            Events.GettingTwin(identity.Id, rid);
            await this.SendUpstreamMessageAsync(RpcCmdPub, topic, this.emptyArray);
            Events.TwinSentWaitingResult(identity.Id, rid);

            await this.WaitCompleted(taskCompletion.Task, identity.Id, rid);
            Events.TwinResultReceived(identity.Id, rid);

            return taskCompletion.Task.Result;
        }

        void HandleRpcAck(string ackedGuid)
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
            if (!long.TryParse(rid, out ridAsLong)) // TODO should use as string itself to make less sensitive to the actual format?
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

            Events.TwinResultReceived(id, ridAsLong);
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
            // TODO acquire the response timeout from the message
            var callingTask = this.edgeHub.InvokeMethodAsync(rid, new DirectMethodRequest(id, method, payload, TimeSpan.FromMinutes(1)));
            _ = callingTask.ContinueWith(
                    async responseTask =>
                    {
                        // DirectMethodResponse has a 'Status' and 'HttpStatusCode'. If everything is fine, 'Status' contains
                        // the response of the device and HttpStatusCode is 200. In this case we pass back the response of the
                        // device. If something went wrong, then HttpStatusCode is an error (typically 404). In this case we
                        // pass back that value. The value 500/InternalServerError is just a fallback, edgeHub is supposed to
                        // handle errors, so 500 always should be overwritten.
                        var responseCode = Convert.ToInt32(HttpStatusCode.InternalServerError);
                        var responseData = this.emptyArray;
                        if (responseTask.IsCompletedSuccessfully)
                        {
                            var response = responseTask.Result;
                            responseData = response.Data ?? responseData;

                            if (response.HttpStatusCode == HttpStatusCode.OK)
                            {
                                responseCode = response.Status;
                            }
                            else
                            {
                                responseCode = Convert.ToInt32(response.HttpStatusCode);
                            }
                        }

                        var topic = string.Format(DirectMethodResponseTemplate, id, responseCode, rid);
                        await this.SendUpstreamMessageAsync(RpcCmdPub, topic, responseData);
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
            try
            {
                await task.TimeoutAfter(this.responseTimeout);
            }
            catch (TimeoutException)
            {
                Events.UpstreamMessageTimeout(id, rid);
                throw;
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

        async Task SendUpstreamMessageAsync(string command, string topic, byte[] payload)
        {
            var messageId = Guid.NewGuid();

            Events.SendingUpstreamMessage(messageId);

            var taskCompletion = new TaskCompletionSource<bool>();
            var added = this.pendingRpcs.TryAdd(messageId, taskCompletion);

            if (!added)
            {
                Events.CannotAddPendingRpcId(messageId);
                throw new Exception("Cannot add rpc id to the pending list, because the id already exists");
            }

            var rpcTopic = string.Format(RpcTopicTemplate, messageId);
            var rpcPayload = this.GetRpcPayload(command, topic, payload);

            var connector = await this.GetConnector();
            await connector.SendAsync(rpcTopic, rpcPayload);

            Events.SentUpstreamWaitingAck(messageId);

            var completedTask = await Task.WhenAny(taskCompletion.Task, Task.Delay(this.responseTimeout));
            if (completedTask != taskCompletion.Task)
            {
                Events.SentUpstreamTimeout(messageId);
                throw new TimeoutException("Sending upstream message through RPC has timed out.");
            }

            Events.ReceivedConfirmation(messageId);
        }

        byte[] GetRpcPayload(string command, string topic, byte[] payload)
        {
            var rpcPacket = new RpcPacket
                                {
                                    Version = RpcVersion,
                                    Cmd = command,
                                    Topic = topic,
                                    Payload = payload
                                };

            var stream = new MemoryStream();
            using (var writer = new BsonDataWriter(stream))
            {
                var serializer = new JsonSerializer
                                     {
                                         ContractResolver = new DefaultContractResolver
                                         {
                                             NamingStrategy = new CamelCaseNamingStrategy()
                                         }
                                     };

                serializer.Serialize(writer, rpcPacket);
            }

            return stream.ToArray();
        }

        void HandleConnectivityEvent(byte[] payload)
        {
            try
            {
                var payloadAsString = Encoding.UTF8.GetString(payload);
                var connectivityEvent = JsonConvert.DeserializeObject<ExpandoObject>(payloadAsString) as IDictionary<string, object>;

                var status = default(object);
                if (connectivityEvent.TryGetValue("status", out status))
                {
                    var statusAsString = status as string;
                    if (statusAsString != null)
                    {
                        switch (statusAsString)
                        {
                            case "Connected":
                                this.isConnected.Set(true);
                                this.CallConnectivityHandlers(true);
                                break;

                            case "Disconnected":
                                this.isConnected.Set(false);
                                this.CallConnectivityHandlers(false);
                                break;

                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Events.ErrorParsingConnectivityEvent(ex);
            }
        }

        void CallConnectivityHandlers(bool isConnected)
        {
            var currentHandlers = this.ConnectionStatusChangedEvent?.GetInvocationList();

            if (currentHandlers == null)
            {
                return;
            }

            foreach (var handler in currentHandlers)
            {
                try
                {
                    handler.DynamicInvoke(isConnected ? CloudConnectionStatus.ConnectionEstablished : CloudConnectionStatus.Disconnected);
                }
                catch (Exception ex)
                {
                    // ignore and go on
                    Events.ErrorDispatchingConnectivityEvent(ex);
                }
            }
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
            RemovingTwinResultSubscription,
            AddingDirectMethodCallSubscription,
            AddingDesiredPropertyUpdateSubscription,

            SendingReportedProperyUpdate,

            ErrorHandlingDownstreamMessage,
            CannotParseGuid,
            CannotFindGuid,

            SendingOpenNotificationForClient,
            SentOpenNotificationForClient,
            ErrorSendingOpenNotificationForClient,
            SendingCloseNotificationForClient,
            SentCloseNotificationForClient,
            ErrorSendingCloseNotificationForClient,
            ErrorParsingConnectivityEvent,
            ErrorDispatchingConnectivityEvent
        }

        public static void GettingTwin(string id, long rid) => Log.LogDebug((int)EventIds.GettingTwin, $"Getting twin for client: {id} with request id: {rid}");
        public static void CannotAddPendingRid(string id, long rid) => Log.LogDebug((int)EventIds.CannotAddPendingRid, $"Cannot register request-id when getting twin for client: {id} with request id: {rid}");
        public static void TwinSentWaitingResult(string id, long rid) => Log.LogDebug((int)EventIds.TwinSentWaitingResult, $"Twin request sent for client: {id} with request id: {rid}, waiting for result");
        public static void TwinUpdateSentWaitingResult(string id, long rid) => Log.LogDebug((int)EventIds.TwinUpdateSentWaitingResult, $"Twin update sent from client: {id} with request id: {rid}, waiting for result");
        public static void TwinUpdateHasConfirmed(string id, long rid) => Log.LogDebug((int)EventIds.TwinUpdateSentWaitingResult, $"Twin update has confirmed for client: {id} with request id: {rid}");
        public static void TwinResultReceived(string id, long rid) => Log.LogDebug((int)EventIds.TwinResultReceived, $"Twin received for client: {id} with request id: {rid}");
        public static void CannotParseRid(string rid) => Log.LogError((int)EventIds.CannotParseGuid, "Cannot parse rid: {rid}");
        public static void CannotFindRid(string rid) => Log.LogError((int)EventIds.CannotFindGuid, "Cannot find rid to ACK: {rid}");
        public static void SendingTelemetry(string id) => Log.LogDebug((int)EventIds.SendingTelemetry, $"Sending telemetry message from client: {id}");

        public static void UpstreamMessageTimeout(string id, long rid) => Log.LogWarning((int)EventIds.UpstreamMessageTimeout, $"Timeout waiting for result after request sent for client: {id} with request id: {rid}");

        public static void SendingReportedProperyUpdate(string id, long rid) => Log.LogDebug((int)EventIds.GettingTwin, $"Sending reported property update for client: {id} with request id: {rid}");

        public static void CannotAddPendingRpcId(Guid guid) => Log.LogWarning((int)EventIds.CannotAddPendingRpcId, $"Cannot add pending rpc id to the pending list, because it already exists. Id: {guid}");

        public static void SendingUpstreamMessage(Guid guid) => Log.LogDebug((int)EventIds.SendingUpstreamMessage, $"Sending message upstream with id: {guid}");
        public static void SentUpstreamWaitingAck(Guid guid) => Log.LogDebug((int)EventIds.SentUpstreamWaitingAck, $"Sent message upstream, waiting for confirmation. Message id: {guid}");
        public static void SentUpstreamTimeout(Guid guid) => Log.LogWarning((int)EventIds.SentUpstreamTimeout, $"Timeout waiting for upstream message confirmation. Message id: {guid}");
        public static void ReceivedConfirmation(Guid guid) => Log.LogDebug((int)EventIds.ReceivedConfirmation, $"Received confirmation for upstream message. Message id: {guid}");

        public static void AddingDirectMethodCallSubscription(string id) => Log.LogDebug((int)EventIds.AddingDirectMethodCallSubscription, $"Adding direct method call subscriptions for client: {id}");
        public static void AddingDesiredPropertyUpdateSubscription(string id) => Log.LogDebug((int)EventIds.AddingDesiredPropertyUpdateSubscription, $"Adding desired property update subscriptions for client: {id}");
        public static void RemovingDirectMethodCallSubscription(string id) => Log.LogDebug((int)EventIds.RemovingDirectMethodCallSubscription, $"Removing direct method call subscriptions for client: {id}");
        public static void RemovingDesiredPropertyUpdateSubscription(string id) => Log.LogDebug((int)EventIds.RemovingDesiredPropertyUpdateSubscription, $"Removing desired property update subscriptions for client: {id}");
        public static void RemovingTwinResultSubscription(string id) => Log.LogDebug((int)EventIds.RemovingTwinResultSubscription, $"Removing twin result subscriptions for client: {id}");

        public static void ErrorHandlingDownstreamMessage(string topic, Exception e) => Log.LogError((int)EventIds.ErrorHandlingDownstreamMessage, e, $"Error handling downstream message on topic: {topic}");
        public static void CannotParseGuid(string guid) => Log.LogError((int)EventIds.CannotParseGuid, $"Cannot parse guid: {guid}");
        public static void CannotFindGuid(string guid) => Log.LogError((int)EventIds.CannotFindGuid, $"Cannot find guid to ACK: {guid}");
        public static void ErrorParsingConnectivityEvent(Exception ex) => Log.LogError((int)EventIds.ErrorParsingConnectivityEvent, ex, "Error parsing connectivity event");
        public static void ErrorDispatchingConnectivityEvent(Exception ex) => Log.LogError((int)EventIds.ErrorDispatchingConnectivityEvent, ex, "Error dispatching connectivity event");
    }
}
