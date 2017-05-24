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
        readonly IMessageConverter<Message> messageConverter;
        readonly ICloudListener cloudListener;
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        // This is temporary, replace with default method handler when available in Client SDK
        const string MethodName = "*";
        Task receiveMessageTask;

        public CloudReceiver(DeviceClient deviceClient, IMessageConverter<Message> messageConverter, ICloudListener cloudListener, IIdentity identity)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.cloudListener = Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
            this.identity = Preconditions.CheckNotNull(identity);
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
                            IMessage message = this.messageConverter.ToMessage(clientMessage);
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
            return this.deviceClient.SetDesiredPropertyUpdateCallback(OnDesiredPropertyUpdates, this.cloudListener);
        }

        public Task RemoveDesiredPropertyUpdatesAsync()
        {
            //return this.deviceClient.SetDesiredPropertyUpdateCallback(null, null);
            // TODO: update device SDK to unregister callback for desired properties by passing null
            return Task.CompletedTask;
        }

        static Task OnDesiredPropertyUpdates(TwinCollection desiredProperties, object userContext)
        {
            var cloudListener = (ICloudListener)userContext;
            return cloudListener.OnDesiredPropertyUpdates(desiredProperties.ToJson());
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
    }
}
