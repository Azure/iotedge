// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using static System.FormattableString;

    class CloudProxy : ICloudProxy
    {
        readonly IDeviceClient deviceClient;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly Action<string, CloudConnectionStatus> connectionStatusChangedHandler;
        readonly string clientId;
        CloudReceiver cloudReceiver;

        public CloudProxy(IDeviceClient deviceClient, IMessageConverterProvider messageConverterProvider, string clientId, Action<string, CloudConnectionStatus> connectionStatusChangedHandler)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.clientId = Preconditions.CheckNonWhiteSpace(clientId, nameof(clientId));
            if (connectionStatusChangedHandler != null)
            {
                this.connectionStatusChangedHandler = connectionStatusChangedHandler;
            }
        }
        public bool IsActive => this.deviceClient.IsActive;

        public async Task<bool> CloseAsync()
        {
            try
            {
                await (this.cloudReceiver?.CloseAsync() ?? Task.CompletedTask);
                await this.deviceClient.CloseAsync();
                Events.Closed(this);
                return true;
            }
            catch (Exception ex)
            {
                Events.ErrorClosing(this, ex);
                return false;
            }
        }

        public async Task<IMessage> GetTwinAsync()
        {
            Twin twin = await this.deviceClient.GetTwinAsync();
            Events.GetTwin(this);
            IMessageConverter<Twin> converter = this.messageConverterProvider.Get<Twin>();
            return converter.ToMessage(twin);
        }

        public async Task SendMessageAsync(IMessage inputMessage)
        {
            Preconditions.CheckNotNull(inputMessage, nameof(inputMessage));
            IMessageConverter<Message> converter = this.messageConverterProvider.Get<Message>();
            Message message = converter.FromMessage(inputMessage);

            try
            {
                await this.deviceClient.SendEventAsync(message);
                Events.SendMessage(this);
            }
            catch (Exception ex)
            {
                Events.ErrorSendingMessage(this, ex);
                await this.HandleException(ex);
                throw;
            }
        }

        public async Task SendMessageBatchAsync(IEnumerable<IMessage> inputMessages)
        {
            IMessageConverter<Message> converter = this.messageConverterProvider.Get<Message>();
            IEnumerable<Message> messages = Preconditions.CheckNotNull(inputMessages, nameof(inputMessages))
                .Select(inputMessage => converter.FromMessage(inputMessage));
            try
            {
                await this.deviceClient.SendEventBatchAsync(messages);
                Events.SendMessage(this);
            }
            catch (Exception ex)
            {
                Events.ErrorSendingBatchMessage(this, ex);
                await this.HandleException(ex);
                throw;
            }
        }

        public async Task UpdateReportedPropertiesAsync(IMessage reportedPropertiesMessage)
        {
            string reportedPropertiesString = Encoding.UTF8.GetString(reportedPropertiesMessage.Body);
            var reported = JsonConvert.DeserializeObject<TwinCollection>(reportedPropertiesString);
            await this.deviceClient.UpdateReportedPropertiesAsync(reported);
            Events.UpdateReportedProperties(this);
        }

        public void BindCloudListener(ICloudListener cloudListener)
        {
            this.cloudReceiver = new CloudReceiver(this, cloudListener);
            Events.BindCloudListener(this);
        }

        public Task SendFeedbackMessageAsync(string messageId, FeedbackStatus feedbackStatus)
        {
            Preconditions.CheckNonWhiteSpace(messageId, nameof(messageId));
            Events.SendFeedbackMessage(this);
            switch (feedbackStatus)
            {
                case FeedbackStatus.Complete:
                    return this.deviceClient.CompleteAsync(messageId);
                case FeedbackStatus.Abandon:
                    return this.deviceClient.AbandonAsync(messageId);
                case FeedbackStatus.Reject:
                    return this.deviceClient.RejectAsync(messageId);
                default:
                    throw new InvalidOperationException("Feedback status type is not supported");
            }
        }

        public Task SetupCallMethodAsync() => this.cloudReceiver.SetupCallMethodAsync;

        public Task RemoveCallMethodAsync() => this.cloudReceiver.RemoveCallMethodAsync;

        public Task SetupDesiredPropertyUpdatesAsync() => this.cloudReceiver.SetupDesiredPropertyUpdatesAsync();

        public Task RemoveDesiredPropertyUpdatesAsync() => this.cloudReceiver.RemoveDesiredPropertyUpdatesAsync();

        public void StartListening() => this.cloudReceiver.StartListening();

        Task HandleException(Exception ex)
        {
            try
            {
                if (ex is UnauthorizedException)
                {
                    this.connectionStatusChangedHandler(this.clientId, CloudConnectionStatus.DisconnectedTokenExpired);
                }
            }
            catch (Exception e)
            {
                Events.ExceptionInHandleException(ex, e);
            }
            return Task.CompletedTask;
        }

        internal class CloudReceiver
        {
            readonly CloudProxy cloudProxy;
            readonly ICloudListener cloudListener;
            readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            readonly DesiredPropertyUpdateHandler desiredUpdateHandler;
            readonly object receiveMessageLoopLock = new object();
            Option<Task> receiveMessageTask = Option.None<Task>();

            // IotHub has max timeout set to 5 minutes, add 30 seconds to make sure it doesn't timeout before IotHub
            static readonly TimeSpan DeviceMethodMaxResponseTimeout = TimeSpan.FromSeconds(5 * 60 + 30);
            // Timeout for receive message because the default timeout is too long (4 minutes) for the case when the connection is closed
            static readonly TimeSpan ReceiveMessageTimeout = TimeSpan.FromSeconds(20);

            public CloudReceiver(CloudProxy cloudProxy, ICloudListener cloudListener)
            {
                this.cloudProxy = Preconditions.CheckNotNull(cloudProxy, nameof(cloudProxy));
                this.cloudListener = Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
                IMessageConverter<TwinCollection> converter = cloudProxy.messageConverterProvider.Get<TwinCollection>();
                this.desiredUpdateHandler = new DesiredPropertyUpdateHandler(cloudListener, converter);
            }

            public void StartListening()
            {
                if (!this.receiveMessageTask.HasValue)
                {
                    lock (this.receiveMessageLoopLock)
                    {
                        if (!this.receiveMessageTask.HasValue)
                        {
                            Events.StartListening(this.cloudProxy.clientId);
                            this.receiveMessageTask = Option.Some(this.SetupMessageListening());
                        }
                    }
                }
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
                            clientMessage = await this.cloudProxy.deviceClient.ReceiveAsync(ReceiveMessageTimeout);
                            if (clientMessage != null)
                            {
                                Events.MessageReceived(this.cloudProxy.clientId);
                                IMessageConverter<Message> converter = this.cloudProxy.messageConverterProvider.Get<Message>();
                                IMessage message = converter.ToMessage(clientMessage);
                                // TODO: check if message delivery count exceeds the limit?
                                await this.cloudListener.ProcessMessageAsync(message);
                            }
                        }
                        catch (Exception e)
                        {
                            if (e is UnauthorizedException)
                            {
                                throw;
                            }
                            // continue when the client times out
                            // TODO: should limit the timeout?
                            Events.ErrorReceivingMessage(this.cloudProxy.clientId, e);
                        }
                    }
                    Events.ReceiverStopped(this.cloudProxy.clientId);
                }
                catch (Exception ex)
                {
                    Events.TerminatingErrorReceivingMessage(this.cloudProxy.clientId, ex);
                    this.receiveMessageTask = Option.None<Task>();
                    await this.cloudProxy.HandleException(ex);
                }
                finally
                {
                    clientMessage?.Dispose();
                }
            }

            public Task CloseAsync()
            {
                Events.Closing(this.cloudProxy.clientId);
                this.cancellationTokenSource.Cancel();
                return this.receiveMessageTask.GetOrElse(Task.CompletedTask);
            }

            public Task SetupCallMethodAsync => this.cloudProxy.deviceClient.SetMethodDefaultHandlerAsync(this.MethodCallHandler, null);

            public Task RemoveCallMethodAsync => this.cloudProxy.deviceClient.SetMethodDefaultHandlerAsync(null, null);

            internal async Task<MethodResponse> MethodCallHandler(MethodRequest methodrequest, object usercontext)
            {
                Preconditions.CheckNotNull(methodrequest, nameof(methodrequest));

                Events.MethodCallReceived(this.cloudProxy.clientId);

                var direceMethodRequest = new DirectMethodRequest(this.cloudProxy.clientId, methodrequest.Name, methodrequest.Data, DeviceMethodMaxResponseTimeout);
                DirectMethodResponse directMethodResponse = await this.cloudListener.CallMethodAsync(direceMethodRequest);
                MethodResponse methodResponse = directMethodResponse.Data == null ? new MethodResponse(directMethodResponse.Status) : new MethodResponse(directMethodResponse.Data, directMethodResponse.Status);
                return methodResponse;
            }

            public Task SetupDesiredPropertyUpdatesAsync() => this.cloudProxy.deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyUpdates, this.desiredUpdateHandler);

            public Task RemoveDesiredPropertyUpdatesAsync()
            {
                // return this.deviceClient.SetDesiredPropertyUpdateCallback(null, null);
                // TODO: update device SDK to unregister callback for desired properties by passing null
                return Task.CompletedTask;
            }

            static Task OnDesiredPropertyUpdates(TwinCollection desiredProperties, object userContext)
            {
                var handler = (DesiredPropertyUpdateHandler)userContext;
                return handler.OnDesiredPropertyUpdates(desiredProperties);
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

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CloudProxy>();
            const int IdStart = CloudProxyEventIds.CloudProxy;

            enum EventIds
            {
                Close = IdStart,
                CloseError,
                GetTwin,
                SendMessage,
                SendMessageError,
                SendMessageBatchError,
                UpdateReportedProperties,
                BindCloudListener,
                SendFeedbackMessage,
                ExceptionInHandleException,
                ClosingReceiver,
                MessageReceived,
                ReceiveError,
                ReceiverStopped,
                MethodReceived,
                StartListening
            }

            public static void Closed(CloudProxy cloudProxy)
            {
                Log.LogInformation((int)EventIds.Close, Invariant($"Closed cloud proxy for device {cloudProxy.clientId}"));
            }

            public static void ErrorClosing(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogError((int)EventIds.CloseError, ex, Invariant($"Error closing cloud proxy for device {cloudProxy.clientId}"));
            }

            public static void GetTwin(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.GetTwin, Invariant($"Getting twin for device {cloudProxy.clientId}"));
            }

            public static void SendMessage(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.SendMessage, Invariant($"Sending message for device {cloudProxy.clientId}"));
            }

            public static void ErrorSendingMessage(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogDebug((int)EventIds.SendMessageError, ex, Invariant($"Error sending message for device {cloudProxy.clientId}"));
            }

            public static void ErrorSendingBatchMessage(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogDebug((int)EventIds.SendMessageBatchError, ex, Invariant($"Error sending message batch for device {cloudProxy.clientId}"));
            }

            public static void UpdateReportedProperties(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.UpdateReportedProperties, Invariant($"Updating reported properties for device {cloudProxy.clientId}"));
            }

            public static void BindCloudListener(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.BindCloudListener, Invariant($"Binding cloud listener for device {cloudProxy.clientId}"));
            }

            public static void SendFeedbackMessage(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.SendFeedbackMessage, Invariant($"Sending feedback message for device {cloudProxy.clientId}"));
            }

            internal static void ExceptionInHandleException(Exception handlingException, Exception caughtException)
            {
                Log.LogDebug((int)EventIds.ExceptionInHandleException, Invariant($"Got exception {caughtException} while handling exception {handlingException}"));
            }

            public static void MessageReceived(string clientId)
            {
                Log.LogDebug((int)EventIds.MessageReceived, Invariant($"Received message from cloud for device {clientId}"));
            }

            public static void Closing(string clientId)
            {
                Log.LogInformation((int)EventIds.ClosingReceiver, Invariant($"Closing receiver for device {clientId}"));
            }

            public static void ErrorReceivingMessage(string clientId, Exception ex)
            {
                Log.LogError((int)EventIds.ReceiveError, ex, Invariant($"Error receiving message for device {clientId}"));
            }

            public static void ReceiverStopped(string clientId)
            {
                Log.LogInformation((int)EventIds.ReceiverStopped, Invariant($"Cloud message receiver stopped for device {clientId}"));
            }

            public static void MethodCallReceived(string clientId)
            {
                Log.LogDebug((int)EventIds.MethodReceived, Invariant($"Received call method from cloud for device {clientId}"));
            }

            public static void StartListening(string clientId)
            {
                Log.LogInformation((int)EventIds.StartListening, Invariant($"Start listening for C2D messages for device {clientId}"));
            }

            internal static void TerminatingErrorReceivingMessage(string clientId, Exception e)
            {
                Log.LogInformation((int)EventIds.ReceiveError, e, Invariant($"Error receiving C2D messages for device {clientId}. Closing receive loop."));
            }
        }
    }
}
