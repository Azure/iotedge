// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class CloudReceiver
    {
        readonly DeviceClient deviceClient;
        readonly IMessageConverter<Message> messageConverter;
        readonly ICloudListener cloudListener;
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        readonly ILogger logger = Logger.Factory.CreateLogger<CloudReceiver>();
        // This is temporary, replace with default method handler when available in Client SDK
        const string MethodName = "*";
        Task receiveMessageTask;

        public CloudReceiver(DeviceClient deviceClient, IMessageConverter<Message> messageConverter, ICloudListener cloudListener)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.cloudListener = Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
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
                            IMessage message = this.messageConverter.ToMessage(clientMessage);
                            // TODO: check if message delivery count exceeds the limit?
                            await this.cloudListener.ProcessMessageAsync(message);
                        }
                    }
                    catch (Exception e)
                    {
                        // continue when the client times out
                        // TODO: should limit the timeout?
                        this.logger.LogError("Error receiving message from the cloud: {0}", e);
                    }
                }
                this.logger.LogInformation("Cloud receiver stopped");
            }
            finally
            {
                clientMessage?.Dispose();
            }
        }

        public Task CloseAsync()
        {
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
    }
}
