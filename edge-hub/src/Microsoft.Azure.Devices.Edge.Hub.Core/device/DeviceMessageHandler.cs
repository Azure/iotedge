// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Device
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    class DeviceMessageHandler : IDeviceListener, IDeviceProxy
    {
        readonly ConcurrentDictionary<string, TaskCompletionSource<DirectMethodResponse>> methodCallTaskCompletionSources = new ConcurrentDictionary<string, TaskCompletionSource<DirectMethodResponse>>();
        readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> messageTaskCompletionSources = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();

        readonly IEdgeHub edgeHub;
        readonly IConnectionManager connectionManager;
        readonly ICloudProxy cloudProxy;
        IDeviceProxy underlyingProxy;

        // IoTHub error codes
        const int GatewayTimeoutErrorCode = 504101;
        const int GenericBadRequest = 400000;

        public DeviceMessageHandler(IIdentity identity, IEdgeHub edgeHub, IConnectionManager connectionManager, ICloudProxy cloudProxy)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.edgeHub = Preconditions.CheckNotNull(edgeHub, nameof(edgeHub));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
            this.cloudProxy = Preconditions.CheckNotNull(cloudProxy, nameof(cloudProxy));
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
            ICloudListener cloudListener = new CloudListener(this.edgeHub, this.Identity.Id);
            this.cloudProxy.BindCloudListener(cloudListener);
            // This operation is async, but we cannot await in this sync method.
            // It is fine because the await part of the operation is cleanup and best effort. 
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

        public Task ProcessMessageFeedbackAsync(string messageId, FeedbackStatus feedbackStatus)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                Events.MessageFeedbackWithNoMessageId(this.Identity);
                return Task.CompletedTask;
            }

            Events.MessageFeedbackReceived(this.Identity, messageId, feedbackStatus);
            if (this.messageTaskCompletionSources.TryRemove(messageId, out TaskCompletionSource<bool> taskCompletionSource))
            {
                if (feedbackStatus == FeedbackStatus.Complete)
                {
                    taskCompletionSource.SetResult(true);
                }
                else
                {
                    taskCompletionSource.SetException(new EdgeHubIOException($"Message not completed by client {this.Identity.Id}"));
                }
                return Task.CompletedTask;
            }
            else
            {
                return this.cloudProxy.SendFeedbackMessageAsync(messageId, feedbackStatus);
            }
        }

        public Task<IMessage> GetTwinAsync() => this.edgeHub.GetTwinAsync(this.Identity.Id);

        public Task ProcessDeviceMessageAsync(IMessage message) => this.edgeHub.ProcessDeviceMessage(this.Identity, message);

        public Task ProcessDeviceMessageBatchAsync(IEnumerable<IMessage> messages) => this.edgeHub.ProcessDeviceMessageBatch(this.Identity, messages);

        public Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage)
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
            return this.edgeHub.UpdateReportedPropertiesAsync(this.Identity, reportedPropertiesMessage);
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
            string lockToken = Guid.NewGuid().ToString();
            message.SystemProperties[SystemProperties.LockToken] = lockToken;

            var taskCompletionSource = new TaskCompletionSource<bool>();
            this.messageTaskCompletionSources.TryAdd(lockToken, taskCompletionSource);

            await this.underlyingProxy.SendMessageAsync(message, input);

            Task completedTask = await Task.WhenAny(taskCompletionSource.Task, Task.Delay(TimeSpan.FromSeconds(60)));
            if (completedTask != taskCompletionSource.Task)
            {
                Events.MessageFeedbackTimedout(this.Identity, lockToken);
                taskCompletionSource.SetException(new TimeoutException("Message completion response not received"));
                this.messageTaskCompletionSources.TryRemove(lockToken, out taskCompletionSource);
            }

            await taskCompletionSource.Task;
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
                taskCompletion.TrySetResult(new DirectMethodResponse(null, null, GatewayTimeoutErrorCode));
                this.methodCallTaskCompletionSources.TryRemove(request.CorrelationId.ToLowerInvariant(), out taskCompletion);
            }

            return await taskCompletion.Task;
        }

        public Task OnDesiredPropertyUpdates(IMessage desiredProperties) => this.underlyingProxy.OnDesiredPropertyUpdates(desiredProperties);

        public Task CloseAsync(Exception ex) => this.underlyingProxy.CloseAsync(ex);

        public void SetInactive() => this.underlyingProxy.SetInactive();

        public bool IsActive => this.underlyingProxy.IsActive;

        #endregion

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceMessageHandler>();
            const int IdStart = HubCoreEventIds.DeviceListener;

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
                MessageFeedbackWithNoMessageId
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
        }
    }
}
