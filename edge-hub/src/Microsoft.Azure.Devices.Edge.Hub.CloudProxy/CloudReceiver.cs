// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util;
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
        // This is temporary, replace with default method handler when available in Client SDK
        const string MethodName = "*";
        Task receiveMessageTask;

        public CloudReceiver(DeviceClient deviceClient, IMessageConverterProvider messageConverterProvider,
            ICloudListener cloudListener, IIdentity identity)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.cloudListener = Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.identity = Preconditions.CheckNotNull(identity);
            var converter = this.messageConverterProvider.Get<TwinCollection>();
            this.desiredUpdateHandler = new DesiredPropertyUpdateHandler(cloudListener, converter);
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
            return this.deviceClient.SetMethodHandlerAsync(MethodName, this.CloudMethodCall, null);
        }

        public Task RemoveCallMethodAsync()
        {
            return this.deviceClient.SetMethodHandlerAsync(MethodName, null, null);
        }

        Task<MethodResponse> CloudMethodCall(MethodRequest methodrequest, object usercontext)
        {
            // TODO - to be implemented
            return Task.FromResult(new MethodResponse(200));
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
                ReceiverStopped
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
