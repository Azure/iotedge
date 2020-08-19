// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Threading.Tasks;
    using App.Metrics.Infrastructure;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    class DeviceMessageHandler : IDeviceListener, IDeviceProxy
    {
        const int GenericBadRequest = 400000;

        readonly ConcurrentDictionary<string, TaskCompletionSource<DirectMethodResponse>> methodCallTaskCompletionSources = new ConcurrentDictionary<string, TaskCompletionSource<DirectMethodResponse>>();
        readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> messageTaskCompletionSources = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();
        readonly ConcurrentDictionary<string, bool> c2dMessageTaskCompletionSources = new ConcurrentDictionary<string, bool>();

        readonly IEdgeHub edgeHub;
        readonly IConnectionManager connectionManager;
        readonly TimeSpan messageAckTimeout;
        readonly AsyncLock serializeMessagesLock = new AsyncLock();
        readonly Option<string> modelId;
        IDeviceProxy underlyingProxy;

        public DeviceMessageHandler(IIdentity identity, IEdgeHub edgeHub, IConnectionManager connectionManager, TimeSpan messageAckTimeout, Option<string> modelId)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.edgeHub = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.messageAckTimeout = messageAckTimeout;
            this.modelId = modelId;
        }

        public IIdentity Identity { get; }

        public Task ProcessMethodResponseAsync(IMessage message)
        {
            Preconditions.CheckNotNull(message, nameof(message));
            if (!message.Properties.TryGetValue(SystemProperties.CorrelationId, out string correlationId))
            {
                Events.MethodResponseInvalid(this.Identity);
                return Task.CompletedTask;
            }

            if (this.methodCallTaskCompletionSources.TryRemove(correlationId.ToLowerInvariant(), out TaskCompletionSource<DirectMethodResponse> taskCompletion))
            {
                DirectMethodResponse directMethodResponse = !message.Properties.TryGetValue(SystemProperties.StatusCode, out string statusCode)
                                                            || !int.TryParse(statusCode, out int statusCodeValue)
                    ? new DirectMethodResponse(correlationId, null, GenericBadRequest)
                    : new DirectMethodResponse(correlationId, message.Body, statusCodeValue);

                Events.MethodResponseReceived(correlationId);
                taskCompletion.SetResult(directMethodResponse);
            }
            else
            {
                Events.MethodResponseNotMapped(correlationId);
            }

            return Task.CompletedTask;
        }

        public void BindDeviceProxy(IDeviceProxy deviceProxy)
        {
            this.underlyingProxy = Preconditions.CheckNotNull(deviceProxy);
            this.connectionManager.AddDeviceConnection(this.Identity, this);
            Events.BindDeviceProxy(this.Identity);
        }

        public async Task CloseAsync()
        {
            if (this.underlyingProxy.IsActive)
            {
                this.SetInactive();
                await this.connectionManager.RemoveDeviceConnection(this.Identity.Id);
                Events.Close(this.Identity);
            }
        }

        public async Task ProcessMessageFeedbackAsync(string messageId, FeedbackStatus feedbackStatus)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                Events.MessageFeedbackWithNoMessageId(this.Identity);
                return;
            }

            Events.MessageFeedbackReceived(this.Identity, messageId, feedbackStatus);
            if (this.messageTaskCompletionSources.TryRemove(messageId, out TaskCompletionSource<bool> taskCompletionSource))
            {
                if (feedbackStatus == FeedbackStatus.Complete)
                {
                    // TaskCompletionSource.SetResult causes the continuation of the underlying task
                    // to happen on the same thread. So calling Task.Yield to let go of the current thread
                    // That way the underlying task will be scheduled to complete asynchronously on a different thread
                    await Task.Yield();
                    taskCompletionSource.SetResult(true);
                }
                else
                {
                    taskCompletionSource.SetException(new EdgeHubMessageRejectedException($"Message not completed by client {this.Identity.Id}"));
                }
            }
            else if (this.c2dMessageTaskCompletionSources.TryRemove(messageId, out bool value) && value)
            {
                Events.ReceivedC2DFeedbackMessage(this.Identity, messageId);
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(this.Identity.Id);
                await cloudProxy.ForEachAsync(cp => cp.SendFeedbackMessageAsync(messageId, feedbackStatus));
            }
            else
            {
                Events.UnknownFeedbackMessage(this.Identity, messageId, feedbackStatus);
            }
        }

        public Task AddSubscription(DeviceSubscription subscription)
            => this.edgeHub.AddSubscription(this.Identity.Id, subscription);

        public Task RemoveSubscription(DeviceSubscription subscription)
            => this.edgeHub.RemoveSubscription(this.Identity.Id, subscription);

        public async Task AddDesiredPropertyUpdatesSubscription(string correlationId)
        {
            await this.edgeHub.AddSubscription(this.Identity.Id, DeviceSubscription.DesiredPropertyUpdates);
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                IMessage responseMessage = new EdgeMessage.Builder(new byte[0])
                    .SetSystemProperties(
                        new Dictionary<string, string>
                        {
                            [SystemProperties.CorrelationId] = correlationId,
                            [SystemProperties.StatusCode] = ((int)HttpStatusCode.OK).ToString()
                        })
                    .Build();
                await this.SendTwinUpdate(responseMessage);
            }
        }

        public async Task RemoveDesiredPropertyUpdatesSubscription(string correlationId)
        {
            await this.edgeHub.RemoveSubscription(this.Identity.Id, DeviceSubscription.DesiredPropertyUpdates);
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                IMessage responseMessage = new EdgeMessage.Builder(new byte[0])
                    .SetSystemProperties(
                        new Dictionary<string, string>
                        {
                            [SystemProperties.CorrelationId] = correlationId,
                            [SystemProperties.StatusCode] = ((int)HttpStatusCode.OK).ToString()
                        })
                    .Build();
                await this.SendTwinUpdate(responseMessage);
            }
        }

        public async Task SendGetTwinRequest(string correlationId)
        {
            try
            {
                using (Metrics.TimeGetTwin(this.Identity.Id))
                {
                    IMessage twin = await this.edgeHub.GetTwinAsync(this.Identity.Id);
                    twin.SystemProperties[SystemProperties.CorrelationId] = correlationId;
                    twin.SystemProperties[SystemProperties.StatusCode] = ((int)HttpStatusCode.OK).ToString();
                    await this.SendTwinUpdate(twin);
                    Events.ProcessedGetTwin(this.Identity.Id);
                    Metrics.AddGetTwin(this.Identity.Id);
                }
            }
            catch (Exception e)
            {
                Events.ErrorGettingTwin(this.Identity, e);
                await this.HandleTwinOperationException(correlationId, e);
            }
        }

        public Task ProcessDeviceMessageAsync(IMessage message)
        {
            this.modelId.ForEach(modelId => message.SystemProperties[SystemProperties.ModelId] = modelId);
            return this.edgeHub.ProcessDeviceMessage(this.Identity, message);
        }

        public Task ProcessDeviceMessageBatchAsync(IEnumerable<IMessage> messages)
        {
            this.modelId.ForEach(modelId =>
            {
                foreach (IMessage message in messages)
                {
                    message.SystemProperties[SystemProperties.ModelId] = modelId;
                }
            });

            return this.edgeHub.ProcessDeviceMessageBatch(this.Identity, messages);
        }

        public async Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage, string correlationId)
        {
            reportedPropertiesMessage.SystemProperties[SystemProperties.EnqueuedTime] = DateTime.UtcNow.ToString("o");
            reportedPropertiesMessage.SystemProperties[SystemProperties.MessageSchema] = Constants.TwinChangeNotificationMessageSchema;
            reportedPropertiesMessage.SystemProperties[SystemProperties.MessageType] = Constants.TwinChangeNotificationMessageType;

            switch (this.Identity)
            {
                case IModuleIdentity moduleIdentity:
                    reportedPropertiesMessage.SystemProperties[SystemProperties.ConnectionDeviceId] = moduleIdentity.DeviceId;
                    reportedPropertiesMessage.SystemProperties[SystemProperties.ConnectionModuleId] = moduleIdentity.ModuleId;
                    break;
                case IDeviceIdentity deviceIdentity:
                    reportedPropertiesMessage.SystemProperties[SystemProperties.ConnectionDeviceId] = deviceIdentity.DeviceId;
                    break;
            }

            try
            {
                using (Metrics.TimeReportedPropertiesUpdate(this.Identity.Id))
                {
                    await this.edgeHub.UpdateReportedPropertiesAsync(this.Identity, reportedPropertiesMessage);
                    Metrics.AddUpdateReportedProperties(this.Identity.Id);
                    if (!string.IsNullOrWhiteSpace(correlationId))
                    {
                        IMessage responseMessage = new EdgeMessage.Builder(new byte[0])
                            .SetSystemProperties(
                                new Dictionary<string, string>
                                {
                                    [SystemProperties.CorrelationId] = correlationId,
                                    [SystemProperties.EnqueuedTime] = DateTime.UtcNow.ToString("o"),
                                    [SystemProperties.StatusCode] = ((int)HttpStatusCode.NoContent).ToString()
                                })
                            .Build();
                        await this.SendTwinUpdate(responseMessage);
                    }
                }
            }
            catch (Exception e)
            {
                Events.ErrorUpdatingReportedPropertiesTwin(this.Identity, e);
                await this.HandleTwinOperationException(correlationId, e);
            }
        }

        async Task HandleTwinOperationException(string correlationId, Exception e)
        {
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                int statusCode = e is InvalidOperationException || e is ArgumentException
                    ? (int)HttpStatusCode.BadRequest
                    : (int)HttpStatusCode.InternalServerError;

                IMessage responseMessage = new EdgeMessage.Builder(new byte[0])
                    .SetSystemProperties(
                        new Dictionary<string, string>
                        {
                            [SystemProperties.CorrelationId] = correlationId,
                            [SystemProperties.EnqueuedTime] = DateTime.UtcNow.ToString("o"),
                            [SystemProperties.StatusCode] = statusCode.ToString()
                        })
                    .Build();
                await this.SendTwinUpdate(responseMessage);
            }
        }

        static class Events
        {
            const int IdStart = HubCoreEventIds.DeviceListener;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceMessageHandler>();

            enum EventIds
            {
                BindDeviceProxy = IdStart,
                RemoveDeviceConnection,
                MethodSentToClient,
                MethodResponseReceived,
                MethodRequestIdNotMatched,
                MethodResponseTimedout,
                InvalidMethodResponse,
                MessageFeedbackTimedout,
                MessageFeedbackReceived,
                MessageFeedbackWithNoMessageId,
                MessageSentToClient,
                ErrorGettingTwin,
                ErrorUpdatingReportedProperties,
                ProcessedGetTwin,
                C2dNoLockToken,
                SendingC2DMessage,
                ReceivedC2DMessageWithSameToken,
                ReceivedC2DFeedbackMessage,
                UnknownFeedbackMessage
            }

            public static void BindDeviceProxy(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.BindDeviceProxy, Invariant($"Bind device proxy for device {identity.Id}"));
            }

            public static void Close(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.RemoveDeviceConnection, Invariant($"Remove device connection for device {identity.Id}"));
            }

            public static void MethodResponseInvalid(IIdentity identity)
            {
                Log.LogError((int)EventIds.InvalidMethodResponse, Invariant($"Method response does not contain required property CorrelationId for device Id {identity.Id}"));
            }

            public static void MethodResponseReceived(string correlationId)
            {
                Log.LogDebug((int)EventIds.MethodResponseReceived, Invariant($"Received response for method invoke call with correlation ID {correlationId}"));
            }

            public static void MethodResponseNotMapped(string correlationId)
            {
                Log.LogDebug((int)EventIds.MethodRequestIdNotMatched, Invariant($"No request found for method invoke response with correlation ID {correlationId}"));
            }

            public static void MethodResponseTimedout(IIdentity identity, string id, string correlationId)
            {
                Log.LogWarning((int)EventIds.MethodResponseTimedout, Invariant($"Did not receive response for method invoke call from device/module {identity.Id} for {id} with correlation ID {correlationId}"));
            }

            public static void MessageFeedbackTimedout(IIdentity identity, string lockToken)
            {
                Log.LogWarning((int)EventIds.MessageFeedbackTimedout, Invariant($"Did not receive ack for message {lockToken} from device/module {identity.Id}"));
            }

            public static void MessageFeedbackReceived(IIdentity identity, string lockToken, FeedbackStatus status)
            {
                Log.LogDebug((int)EventIds.MessageFeedbackReceived, Invariant($"Received feedback {status} for message {lockToken} from device/module {identity.Id}"));
            }

            public static void MessageFeedbackWithNoMessageId(IIdentity identity)
            {
                Log.LogWarning((int)EventIds.MessageFeedbackWithNoMessageId, Invariant($"Received feedback message with no message Id from device/module {identity.Id}"));
            }

            public static void MethodCallSentToClient(IIdentity identity, string id, string correlationId)
            {
                Log.LogDebug((int)EventIds.MethodSentToClient, Invariant($"Sent method invoke call from device/module {identity.Id} for {id} with correlation ID {correlationId}"));
            }

            public static void SendingMessage(IIdentity identity, string lockToken)
            {
                Log.LogDebug((int)EventIds.MessageSentToClient, Invariant($"Sent message with correlation ID {lockToken} to {identity.Id}"));
            }

            public static void ErrorGettingTwin(IIdentity identity, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorGettingTwin, ex, Invariant($"Error getting twin for {identity.Id}"));
            }

            public static void ErrorUpdatingReportedPropertiesTwin(IIdentity identity, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorUpdatingReportedProperties, ex, Invariant($"Error updating reported properties for {identity.Id}"));
            }

            public static void ProcessedGetTwin(string identityId)
            {
                Log.LogDebug((int)EventIds.ProcessedGetTwin, Invariant($"Processed GetTwin for {identityId}"));
            }

            public static void C2dNoLockToken(IIdentity identity)
            {
                Log.LogWarning((int)EventIds.C2dNoLockToken, Invariant($"Received C2D message for {identity.Id} with no lock token. Abandoning message."));
            }

            public static void SendingC2DMessage(IIdentity identity, string lockToken)
            {
                Log.LogDebug((int)EventIds.SendingC2DMessage, Invariant($"Sending C2D message with lock token {lockToken} to {identity.Id}"));
            }

            public static void ReceivedC2DMessageWithSameToken(IIdentity identity, string lockToken)
            {
                Log.LogWarning((int)EventIds.ReceivedC2DMessageWithSameToken, Invariant($"Received duplicate C2D message for {identity.Id} with the same lock token {lockToken}. Abandoning message."));
            }

            public static void ReceivedC2DFeedbackMessage(IIdentity identity, string messageId)
            {
                Log.LogDebug((int)EventIds.ReceivedC2DFeedbackMessage, Invariant($"Received C2D feedback message from {identity.Id} with lock token {messageId}"));
            }

            public static void UnknownFeedbackMessage(IIdentity identity, string messageId, FeedbackStatus feedbackStatus)
            {
                Log.LogWarning((int)EventIds.UnknownFeedbackMessage, Invariant($"Received unknown feedback message from {identity.Id} with lock token {messageId} and status {feedbackStatus}. Abandoning message."));
            }
        }

        #region IDeviceProxy

        public Task SendC2DMessageAsync(IMessage message)
        {
            if (!message.SystemProperties.TryGetValue(SystemProperties.LockToken, out string lockToken) || string.IsNullOrWhiteSpace(lockToken))
            {
                // TODO - Should we throw here instead?
                Events.C2dNoLockToken(this.Identity);
                return Task.CompletedTask;
            }

            Events.SendingC2DMessage(this.Identity, lockToken);
            if (!this.c2dMessageTaskCompletionSources.TryAdd(lockToken, true))
            {
                Events.ReceivedC2DMessageWithSameToken(this.Identity, lockToken);
                return Task.CompletedTask;
            }

            return this.underlyingProxy.SendC2DMessageAsync(message);
        }

        /// <summary>
        /// This method sends the message to the device, and adds the TaskCompletionSource (that awaits the response) to the messageTaskCompletionSources list.
        /// When the message feedback call comes back, ProcessMessageFeedback sets the TaskCompletionSource value, which results in the awaiting task to be completed.
        /// If no response comes back, then it times out.
        /// </summary>
        public async Task SendMessageAsync(IMessage message, string input)
        {
            // Locking here since multiple queues could be sending to the same module
            // The messages need to be processed in order.
            using (await this.serializeMessagesLock.LockAsync())
            {
                string lockToken = Guid.NewGuid().ToString();
                message.SystemProperties[SystemProperties.LockToken] = lockToken;

                var taskCompletionSource = new TaskCompletionSource<bool>();
                this.messageTaskCompletionSources.TryAdd(lockToken, taskCompletionSource);

                using (Metrics.TimeMessageSend(this.Identity, message, message.GetOutput(), input))
                {
                    Events.SendingMessage(this.Identity, lockToken);
                    Metrics.MessageProcessingLatency(this.Identity, message);
                    await this.underlyingProxy.SendMessageAsync(message, input);

                    Task completedTask = await Task.WhenAny(taskCompletionSource.Task, Task.Delay(this.messageAckTimeout));
                    if (completedTask != taskCompletionSource.Task)
                    {
                        Events.MessageFeedbackTimedout(this.Identity, lockToken);
                        taskCompletionSource.SetException(new TimeoutException("Message completion response not received"));
                        this.messageTaskCompletionSources.TryRemove(lockToken, out taskCompletionSource);
                    }

                    await taskCompletionSource.Task;
                }
            }
        }

        /// <summary>
        /// This method invokes the method on the device, and adds the TaskCompletionSource (that awaits the response) to the methodCallTaskCompletionSources list.
        /// When the response comes back, SendMethodResponse sets the TaskCompletionSource value, which results in the awaiting task to be completed.
        /// If no response comes back, then it times out.
        /// </summary>
        public async Task<DirectMethodResponse> InvokeMethodAsync(DirectMethodRequest request)
        {
            var taskCompletion = new TaskCompletionSource<DirectMethodResponse>();

            this.methodCallTaskCompletionSources.TryAdd(request.CorrelationId.ToLowerInvariant(), taskCompletion);
            await this.underlyingProxy.InvokeMethodAsync(request);
            Events.MethodCallSentToClient(this.Identity, request.Id, request.CorrelationId);

            Task completedTask = await Task.WhenAny(taskCompletion.Task, Task.Delay(request.ResponseTimeout));
            if (completedTask != taskCompletion.Task)
            {
                Events.MethodResponseTimedout(this.Identity, request.Id, request.CorrelationId);
                taskCompletion.TrySetResult(new DirectMethodResponse(new EdgeHubTimeoutException($"Timed out waiting for device to respond to method request {request.CorrelationId}"), HttpStatusCode.GatewayTimeout));
                this.methodCallTaskCompletionSources.TryRemove(request.CorrelationId.ToLowerInvariant(), out taskCompletion);
            }

            return await taskCompletion.Task;
        }

        public Task OnDesiredPropertyUpdates(IMessage twinUpdates) => this.underlyingProxy.OnDesiredPropertyUpdates(twinUpdates);

        public Task SendTwinUpdate(IMessage twin) => this.underlyingProxy.SendTwinUpdate(twin);

        public Task CloseAsync(Exception ex) => this.underlyingProxy.CloseAsync(ex);

        public void SetInactive() => this.underlyingProxy.SetInactive();

        public bool IsActive => this.underlyingProxy.IsActive;

        public Task<Option<IClientCredentials>> GetUpdatedIdentity() => this.underlyingProxy.GetUpdatedIdentity();

        #endregion

        static class Metrics
        {
            static readonly IMetricsTimer MessagesTimer = Util.Metrics.Metrics.Instance.CreateTimer(
                "message_send_duration_seconds",
                "Time taken to send a message",
                new List<string> { "from", "to", "from_route_output", "to_route_input" });

            static readonly IMetricsTimer GetTwinTimer = Util.Metrics.Metrics.Instance.CreateTimer(
                "gettwin_duration_seconds",
                "Time taken to get twin",
                new List<string> { "source", "id" });

            static readonly IMetricsCounter GetTwinCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                "gettwin",
                "Get twin calls",
                new List<string> { "source", "id", MetricsConstants.MsTelemetry });

            static readonly IMetricsTimer ReportedPropertiesTimer = Util.Metrics.Metrics.Instance.CreateTimer(
                "reported_properties_update_duration_seconds",
                "Time taken to update reported properties",
                new List<string> { "target", "id" });

            static readonly IMetricsCounter ReportedPropertiesCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                "reported_properties",
                "Reported properties update calls",
                new List<string> { "target", "id", MetricsConstants.MsTelemetry });

            static readonly IMetricsDuration MessagesProcessLatency = Util.Metrics.Metrics.Instance.CreateDuration(
                "message_process_duration",
                "Time taken to process message in EdgeHub",
                new List<string> { "from", "to", "priority", MetricsConstants.MsTelemetry });

            public static IDisposable TimeMessageSend(IIdentity identity, IMessage message, string fromRoute, string toRoute)
            {
                string from = message.GetSenderId();
                string to = identity.Id;
                return MessagesTimer.GetTimer(new[] { from, to, fromRoute, toRoute });
            }

            public static IDisposable TimeGetTwin(string id) => GetTwinTimer.GetTimer(new[] { "edge_hub", id });

            public static void AddGetTwin(string id) => GetTwinCounter.Increment(1, new[] { "edge_hub", id, bool.TrueString });

            public static IDisposable TimeReportedPropertiesUpdate(string id) => ReportedPropertiesTimer.GetTimer(new[] { "edge_hub", id });

            public static void AddUpdateReportedProperties(string id) => ReportedPropertiesCounter.Increment(1, new[] { "edge_hub", id, bool.TrueString });

            public static void MessageProcessingLatency(IIdentity identity, IMessage message)
            {
                string from = message.GetSenderId();
                string to = identity.Id;
                string priority = message.ProcessedPriority.ToString();
                if (message.EnqueuedTimestamp != 0)
                {
                    TimeSpan duration = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - message.EnqueuedTimestamp);
                    MessagesProcessLatency.Set(
                        duration.TotalSeconds,
                        new[] { from, to, priority, bool.TrueString });
                }
            }
        }
    }
}
