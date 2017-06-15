// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Routing
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Routing.Core;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using static System.FormattableString;
    using IIdentity = Microsoft.Azure.Devices.Edge.Hub.Core.IIdentity;
    using IMessage = Microsoft.Azure.Devices.Edge.Hub.Core.IMessage;
    using IRoutingMessage = Microsoft.Azure.Devices.Routing.Core.IMessage;

    public class RoutingEdgeHub : IEdgeHub
    {
        readonly Router router;
        readonly Core.IMessageConverter<IRoutingMessage> messageConverter;
        readonly IConnectionManager connectionManager;
        readonly ConcurrentDictionary<string, TaskCompletionSource<DirectMethodResponse>> methodCalls = new ConcurrentDictionary<string, TaskCompletionSource<DirectMethodResponse>>();

        static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

        // Error code used by IotHub when response times out
        const int GatewayTimeoutErrorCode = 504101;

        public RoutingEdgeHub(Router router, Core.IMessageConverter<IRoutingMessage> messageConverter, IConnectionManager connectionManager)
        {
            this.router = Preconditions.CheckNotNull(router, nameof(router));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.connectionManager = Preconditions.CheckNotNull(connectionManager, nameof(connectionManager));
        }

        public Task ProcessDeviceMessage(IIdentity identity, IMessage message)
        {
            IRoutingMessage routingMessage = this.messageConverter.FromMessage(Preconditions.CheckNotNull(message, nameof(message)));
            return this.router.RouteAsync(routingMessage);
        }

        public Task ProcessDeviceMessageBatch(IIdentity identity, IEnumerable<IMessage> messages)
        {
            IEnumerable<IRoutingMessage> routingMessages = Preconditions.CheckNotNull(messages)
                .Select(m => this.messageConverter.FromMessage(m));
            return this.router.RouteAsync(routingMessages);
        }

        public async Task<DirectMethodResponse> InvokeMethodAsync(IIdentity identity, DirectMethodRequest methodRequest)
        {
            Preconditions.CheckNotNull(methodRequest, nameof(methodRequest));

            Events.MethodCallReceived(identity, methodRequest.Id, methodRequest.CorrelationId);

            int retryTimes = (int)(methodRequest.ConnectTimeout.Ticks / RetryInterval.Ticks);
            Option<IDeviceProxy> deviceProxy = retryTimes > 1
                ? await Retry.Do(
                    () => Task.FromResult(this.connectionManager.GetDeviceConnection(methodRequest.Id)),
                    dp => dp.HasValue,
                    ex => true,
                    RetryInterval,
                    retryTimes)
                : this.connectionManager.GetDeviceConnection(methodRequest.Id);

            return await deviceProxy.Match(
                dp => this.InvokeMethodAsync(identity, dp, methodRequest),
                () => Task.FromResult(new DirectMethodResponse(null, null, (int)HttpStatusCode.NotFound)));
        }

        /// <summary>
        /// This method invokes the method on the device, and adds the TaskCompletionSource (that awaits the response) to the methodCalls list.
        /// When the response comes back, SendMethodResponse sets the TaskCompletionSource value, which results in the awaiting task to be completed.
        /// If no response comes back, then it times out.
        /// </summary>
        async Task<DirectMethodResponse> InvokeMethodAsync(IIdentity identity, IDeviceProxy deviceProxy, DirectMethodRequest methodRequest)
        {
            var taskCompletion = new TaskCompletionSource<DirectMethodResponse>();

            this.methodCalls.TryAdd(methodRequest.CorrelationId.ToLowerInvariant(), taskCompletion);
            await deviceProxy.CallMethodAsync(methodRequest);
            Events.MethodCallSentToClient(identity, methodRequest.Id, methodRequest.CorrelationId);

            Task completedTask = await Task.WhenAny(taskCompletion.Task, Task.Delay(methodRequest.ResponseTimeout));
            if (completedTask != taskCompletion.Task)
            {
                Events.MethodResponseTimedout(identity, methodRequest.Id, methodRequest.CorrelationId);
                taskCompletion.TrySetResult(new DirectMethodResponse(null, null, GatewayTimeoutErrorCode));
                this.methodCalls.TryRemove(methodRequest.CorrelationId.ToLowerInvariant(), out taskCompletion);
            }

            return await taskCompletion.Task;
        }

        /// <summary>
        /// Find the TaskCompletionSource corresponding to this response, and set its result (so that the underlying task completes).
        /// </summary>
        public Task SendMethodResponseAsync(DirectMethodResponse response)
        {
            Preconditions.CheckNotNull(response, nameof(response));

            if (this.methodCalls.TryRemove(response.CorrelationId.ToLowerInvariant(), out TaskCompletionSource<DirectMethodResponse> taskCompletion))
            {
                Events.MethodResponseReceived(response.CorrelationId);
                taskCompletion.SetResult(response);
            }
            else
            {
                Events.MethodResponseNotMapped(response.CorrelationId);
            }

            return TaskEx.Done;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<RoutingEdgeHub>();
            const int IdStart = HubCoreEventIds.RoutingEdgeHub;

            enum EventIds
            {
                MessageReceived = IdStart,
                MethodReceived,
                MethodSentToClient,
                MethodResponseReceived,
                MethodRequestIdNotMatched,
                MethodResponseTimedout
            }

            public static void MessageReceived(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.MessageReceived, Invariant($"Received message from device/module with ID {identity.Id}"));
            }


            public static void MethodCallReceived(IIdentity identity, string id, string correlationId)
            {
                Log.LogDebug((int)EventIds.MethodReceived, Invariant($"Received method invoke call from device/module {identity.Id} for {id} with correlation ID {correlationId}"));
            }

            public static void MethodCallSentToClient(IIdentity identity, string id, string correlationId)
            {
                Log.LogDebug((int)EventIds.MethodSentToClient, Invariant($"Sent method invoke call from device/module {identity.Id} for {id} with correlation ID {correlationId}"));
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
                Log.LogDebug((int)EventIds.MethodResponseTimedout, Invariant($"Didn't receive response for method invoke call from device/module {identity.Id} for {id} with correlation ID {correlationId}"));
            }
        }
    }
}