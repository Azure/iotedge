// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    class DeviceMessageHandler : IDeviceListener, IDeviceProxy
    {
        const int GenericBadRequest = 400000;
        static readonly TimeSpan MessageResponseTimeout = TimeSpan.FromSeconds(30);

        readonly ConcurrentDictionary<string, TaskCompletionSource<DirectMethodResponse>> methodCallTaskCompletionSources = new ConcurrentDictionary<string, TaskCompletionSource<DirectMethodResponse>>();
        readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> messageTaskCompletionSources = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();

        readonly IEdgeHub edgeHub;
        readonly IConnectionManager connectionManager;
        readonly AsyncLock serializeMessagesLock = new AsyncLock();
        IDeviceProxy underlyingProxy;

        public DeviceMessageHandler(IIdentity identity, IEdgeHub edgeHub, IConnectionManager connectionManager)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.edgeHub = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
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
                    taskCompletionSource.SetException(new EdgeHubIOException($"Message not completed by client {this.Identity.Id}"));
                }
            }
            else
            {
                Option<ICloudProxy> cloudProxy = await this.connectionManager.GetCloudConnection(this.Identity.Id);
                await cloudProxy.ForEachAsync(cp => cp.SendFeedbackMessageAsync(messageId, feedbackStatus));
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
                IMessage twin = await this.edgeHub.GetTwinAsync(this.Identity.Id);
                twin.SystemProperties[SystemProperties.CorrelationId] = correlationId;
                twin.SystemProperties[SystemProperties.StatusCode] = ((int)HttpStatusCode.OK).ToString();
                await this.SendTwinUpdate(twin);
                Events.ProcessedGetTwin(this.Identity.Id);
            }
            catch (Exception e)
            {
                Events.ErrorGettingTwin(this.Identity, e);
                await this.HandleTwinOperationException(correlationId, e);
            }
        }

        public Task ProcessDeviceMessageAsync(IMessage message) => this.edgeHub.ProcessDeviceMessage(this.Identity, message);

        public Task ProcessDeviceMessageBatchAsync(IEnumerable<IMessage> messages) => this.edgeHub.ProcessDeviceMessageBatch(this.Identity, messages);

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
                await this.edgeHub.UpdateReportedPropertiesAsync(this.Identity, reportedPropertiesMessage);

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
                ProcessedGetTwin
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
        }

        #region IDeviceProxy

        public Task SendC2DMessageAsync(IMessage message) => this.underlyingProxy.SendC2DMessageAsync(message);

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

                Events.SendingMessage(this.Identity, lockToken);
                await this.underlyingProxy.SendMessageAsync(message, input);

                Task completedTask = await Task.WhenAny(taskCompletionSource.Task, Task.Delay(MessageResponseTimeout));
                if (completedTask != taskCompletionSource.Task)
                {
                    Events.MessageFeedbackTimedout(this.Identity, lockToken);
                    taskCompletionSource.SetException(new TimeoutException("Message completion response not received"));
                    this.messageTaskCompletionSources.TryRemove(lockToken, out taskCompletionSource);
                }

                await taskCompletionSource.Task;
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
    }
}
