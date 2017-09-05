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
        // Timeout for receive message because the default timeout is too long (4 minutes) for the case when the connection is closed
        static readonly TimeSpan ReceiveMessageTimeout = TimeSpan.FromSeconds(20);

        public CloudReceiver(DeviceClient deviceClient, IMessageConverterProvider messageConverterProvider,
            ICloudListener cloudListener, IIdentity identity)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.cloudListener = Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.identity = Preconditions.CheckNotNull(identity, nameof(identity));
            IMessageConverter<TwinCollection> converter = this.messageConverterProvider.Get<TwinCollection>();
            this.desiredUpdateHandler = new DesiredPropertyUpdateHandler(cloudListener, converter);
            this.methodCalls = new ConcurrentDictionary<string, TaskCompletionSource<MethodResponse>>();
        }

        public void StartListening()
        {
            Events.StartListening(this);
            this.receiveMessageTask = this.SetupMessageListening();
        }

        async Task SetupMessageListening()
        {
            Message clientMessage = null;
            try
            {
                while (!this.cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        clientMessage = await this.deviceClient.ReceiveAsync(ReceiveMessageTimeout);
                        if (clientMessage != null)
                        {
                            Events.MessageReceived(this);
                            IMessageConverter<Message> converter = this.messageConverterProvider.Get<Message>();
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
            return this.deviceClient.SetMethodDefaultHandlerAsync(this.MethodCallHandler, null);
        }

        public Task RemoveCallMethodAsync()
        {
            return this.deviceClient.SetMethodDefaultHandlerAsync(null, null);
        }

        internal async Task<MethodResponse> MethodCallHandler(MethodRequest methodrequest, object usercontext)
        {
            Preconditions.CheckNotNull(methodrequest, nameof(methodrequest));

            Events.MethodCallReceived(this);

            var direceMethodRequest = new DirectMethodRequest(this.identity.Id, methodrequest.Name, methodrequest.Data, DeviceMethodMaxResponseTimeout);
            DirectMethodResponse directMethodResponse = await this.cloudListener.CallMethodAsync(direceMethodRequest);
            MethodResponse methodResponse = directMethodResponse.Data == null ? new MethodResponse(directMethodResponse.Status) : new MethodResponse(directMethodResponse.Data, directMethodResponse.Status);
            return methodResponse;
        }

        public Task SetupDesiredPropertyUpdatesAsync()
        {
            return this.deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyUpdates, this.desiredUpdateHandler);
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
                MethodResponseReceived,
                MethodRequestIdNotMatched,
                MethodResponseTimedout,
                StartListening
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

            public static void StartListening(CloudReceiver cloudReceiver)
            {
                Log.LogInformation((int)EventIds.StartListening, Invariant($"Start listening for device {cloudReceiver.identity.Id}"));
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
