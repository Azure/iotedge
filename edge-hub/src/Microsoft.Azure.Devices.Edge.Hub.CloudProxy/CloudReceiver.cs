// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    class CloudReceiver
    {
        readonly IIdentity identity;
        readonly DeviceClient deviceClient;
        readonly ICloudListener cloudListener;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly DesiredPropertyUpdateHandler desiredUpdateHandler;
        readonly ConcurrentDictionary<string, TaskCompletionSource<MethodResponse>> methodCalls;
        readonly AtomicLong correlationId = new AtomicLong();

        Task receiveMessageTask;

        // IotHub has max timeout set to 5 minutes, add 30 seconds to make sure it doesn't timeout before IotHub
        static readonly TimeSpan DeviceMethodMaxResponseTimeout = TimeSpan.FromSeconds(5 * 60 + 30);
        // Error code used by IotHub when response times out
        const int GatewayTimeoutErrorCode = 504101;

        // This is temporary, replace with default method handler when available in Client SDK
        const string MethodName = "*";

        public CloudReceiver(DeviceClient deviceClient, IMessageConverterProvider messageConverterProvider,
            ICloudListener cloudListener, IIdentity identity)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.cloudListener = Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.identity = Preconditions.CheckNotNull(identity);
            var converter = this.messageConverterProvider.Get<TwinCollection>();
            this.desiredUpdateHandler = new DesiredPropertyUpdateHandler(cloudListener, converter);
            this.methodCalls = new ConcurrentDictionary<string, TaskCompletionSource<MethodResponse>>();
        }

        public void StartListening()
        {
            this.receiveMessageTask = this.SetupMessageListening();
        }

        async Task SetupMessageListening()
        {
            Client.Message clientMessage = null;
            try
            {
                while (!this.cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        clientMessage = await this.deviceClient.ReceiveAsync();
                        if (clientMessage != null)
                        {
                            Events.MessageReceived(this);
                            var converter = this.messageConverterProvider.Get<Message>();
                            IMessage message = converter.ToMessage(clientMessage);
                            // TODO: check if message delivery count exceeds the limit?
                            await this.cloudListener.ProcessMessageAsync(message);
                        }
                    }
                    catch (Exception e)
                    {
                        // continue when the client times out
                        // TODO: should limit the timeout?
                        Events.ErrorReceivingMessage(this, e);
                    }
                }
                
                Events.ReceiverStopped(this);
            }
            finally
            {
                clientMessage?.Dispose();
            }
        }

        public Task CloseAsync()
        {
            Events.Closing(this);
            this.cancellationTokenSource.Cancel();
            return this.receiveMessageTask ?? TaskEx.Done;
        }

        public Task SetupCallMethodAsync()
        {
            return this.deviceClient.SetMethodHandlerAsync(MethodName, this.MethodCallHandler, null);
        }

        public Task RemoveCallMethodAsync()
        {
            return this.deviceClient.SetMethodHandlerAsync(MethodName, null, null);
        }

        public Task SendMethodResponseAsync(DirectMethodResponse response)
        {
            Preconditions.CheckNotNull(response, nameof(response));

            var deviceClientResponse = new MethodResponse(response.Data, response.Status);

            if (this.methodCalls.TryRemove(response.RequestId.ToLowerInvariant(), out TaskCompletionSource<MethodResponse> taskCompletion))
            {
                Events.MethodResponseReceived(this, response.RequestId);
                taskCompletion.SetResult(deviceClientResponse);
            }
            else
            {
                Events.MethodResponseNotMapped(this, response.RequestId);
            }

            return TaskEx.Done;
        }

        internal async Task<MethodResponse> MethodCallHandler(MethodRequest methodrequest, object usercontext)
        {
            Preconditions.CheckNotNull(methodrequest, nameof(methodrequest));

            Events.MethodCallReceived(this);

            string id = this.correlationId.Increment().ToString();
            var taskCompletion = new TaskCompletionSource<MethodResponse>();

            this.methodCalls.TryAdd(id.ToLowerInvariant(), taskCompletion);
            await this.cloudListener.CallMethodAsync(new DirectMethodRequest(id, methodrequest.Name, methodrequest.Data));
            Events.MethodCallSentToClient(this, id);

            Task completedTask = await Task.WhenAny(taskCompletion.Task, Task.Delay(DeviceMethodMaxResponseTimeout));
            if (completedTask != taskCompletion.Task)
            {
                Events.MethodResponseTimedout(this, id);
                taskCompletion.TrySetResult(new MethodResponse(GatewayTimeoutErrorCode));
                this.methodCalls.TryRemove(id.ToLowerInvariant(), out taskCompletion);
            }

            return await taskCompletion.Task;
        }

        public Task SetupDesiredPropertyUpdatesAsync()
        {
            return this.deviceClient.SetDesiredPropertyUpdateCallback(OnDesiredPropertyUpdates, this.desiredUpdateHandler);
        }

        public Task RemoveDesiredPropertyUpdatesAsync()
        {
            //return this.deviceClient.SetDesiredPropertyUpdateCallback(null, null);
            // TODO: update device SDK to unregister callback for desired properties by passing null
            return Task.CompletedTask;
        }

        static Task OnDesiredPropertyUpdates(TwinCollection desiredProperties, object userContext)
        {
            var handler = (DesiredPropertyUpdateHandler)userContext;
            return handler.OnDesiredPropertyUpdates(desiredProperties);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudReceiver>();
            const int IdStart = CloudProxyEventIds.CloudReceiver;

            enum EventIds
            {
                Closing = IdStart,
                MessageReceived,
                ReceiveError,
                ReceiverStopped,
                MethodReceived,
                MethodSentToClient,
                MethodResponseReceived,
                MethodRequestIdNotMatched,
                MethodResponseTimedout
            }

            public static void MessageReceived(CloudReceiver cloudReceiver)
            {
                Log.LogDebug((int)EventIds.MessageReceived, Invariant($"Received message from cloud for device {cloudReceiver.identity.Id}"));
            }

            public static void Closing(CloudReceiver cloudReceiver)
            {
                Log.LogInformation((int)EventIds.Closing, Invariant($"Closing receiver for device {cloudReceiver.identity.Id}"));
            }

            public static void ErrorReceivingMessage(CloudReceiver cloudReceiver, Exception ex)
            {
                Log.LogError((int)EventIds.ReceiveError, ex, Invariant($"Error receiving message for device {cloudReceiver.identity.Id}"));
            }

            public static void ReceiverStopped(CloudReceiver cloudReceiver)
            {
                Log.LogInformation((int)EventIds.ReceiverStopped, Invariant($"Cloud message receiver stopped for device {cloudReceiver.identity.Id}"));
            }

            public static void MethodCallReceived(CloudReceiver cloudReceiver)
            {
                Log.LogDebug((int)EventIds.MethodReceived, Invariant($"Received call method from cloud for device {cloudReceiver.identity.Id}"));
            }

            public static void MethodCallSentToClient(CloudReceiver cloudReceiver, string requestId)
            {
                Log.LogDebug((int)EventIds.MethodSentToClient, Invariant($"Sent call method from cloud for device {cloudReceiver.identity.Id} with requestId {requestId}"));
            }

            public static void MethodResponseReceived(CloudReceiver cloudReceiver, string requestId)
            {
                Log.LogDebug((int)EventIds.MethodResponseReceived, Invariant($"Received response for call with requestId {requestId} method from device {cloudReceiver.identity.Id}"));
            }

            public static void MethodResponseNotMapped(CloudReceiver cloudReceiver, string requestId)
            {
                Log.LogError((int)EventIds.MethodRequestIdNotMatched, Invariant($"No Request found for received response for call with requestId {requestId} for device {cloudReceiver.identity.Id}"));
            }

            public static void MethodResponseTimedout(CloudReceiver cloudReceiver, string requestId)
            {
                Log.LogDebug((int)EventIds.MethodResponseTimedout, Invariant($"Didn't receive response for call with requestId {requestId} method from device {cloudReceiver.identity.Id}"));
            }
        }

        class DesiredPropertyUpdateHandler
        {
            readonly ICloudListener listener;
            readonly IMessageConverter<TwinCollection> converter;

            public DesiredPropertyUpdateHandler(ICloudListener listener, IMessageConverter<TwinCollection> converter)
            {
                this.listener = listener;
                this.converter = converter;
            }

            public Task OnDesiredPropertyUpdates(TwinCollection desiredProperties)
            {
                return this.listener.OnDesiredPropertyUpdates(this.converter.ToMessage(desiredProperties));
            }
        }
    }
}
