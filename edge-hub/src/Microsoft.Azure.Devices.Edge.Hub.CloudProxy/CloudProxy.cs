// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using static System.FormattableString;

    class CloudProxy : ICloudProxy
    {        
        readonly IIdentity identity;        
        readonly DeviceClient deviceClient;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly AtomicBoolean isActive;
        readonly Action<ConnectionStatus, ConnectionStatusChangeReason> connectionStatusChangedHandler;
        CloudReceiver cloudReceiver;

        public CloudProxy(DeviceClient deviceClient, IMessageConverterProvider messageConverterProvider, IIdentity identity, Action<ConnectionStatus, ConnectionStatusChangeReason> connectionStatusChangedHandler)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.isActive = new AtomicBoolean(true);
            this.identity = Preconditions.CheckNotNull(identity, nameof(identity));
            if (connectionStatusChangedHandler != null)
            {
                this.connectionStatusChangedHandler = connectionStatusChangedHandler;
            }
        }

        public async Task<bool> CloseAsync()
        {
            try
            {
                if (this.isActive.GetAndSet(false))
                {
                    if (this.cloudReceiver != null)
                    {
                        await this.cloudReceiver.CloseAsync();
                    }
                    await this.deviceClient.CloseAsync();
                    this.deviceClient.Dispose();
                    Events.Closed(this);
                }
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

        public bool IsActive => this.isActive.Get();

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

        public Task SetupCallMethodAsync() => this.cloudReceiver.SetupCallMethodAsync();

        public Task RemoveCallMethodAsync() => this.cloudReceiver.RemoveCallMethodAsync();

        public Task SetupDesiredPropertyUpdatesAsync() => this.cloudReceiver.SetupDesiredPropertyUpdatesAsync();

        public Task RemoveDesiredPropertyUpdatesAsync() => this.cloudReceiver.RemoveDesiredPropertyUpdatesAsync();

        public void StartListening() => this.cloudReceiver.StartListening();

        async Task HandleException(Exception ex)
        {
            try
            {
                if (ex is UnauthorizedException)
                {
                    await this.CloseAsync();
                    this.connectionStatusChangedHandler(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.Expired_SAS_Token);
                }
            }
            catch (Exception e)
            {
                Events.ExceptionInHandleException(ex, e);
            }
        }

        internal class CloudReceiver
        {
            readonly CloudProxy cloudProxy;
            readonly ICloudListener cloudListener;
            readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            readonly DesiredPropertyUpdateHandler desiredUpdateHandler;
            readonly ConcurrentDictionary<string, TaskCompletionSource<MethodResponse>> methodCalls;
            readonly AtomicLong correlationId = new AtomicLong();
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
                this.methodCalls = new ConcurrentDictionary<string, TaskCompletionSource<MethodResponse>>();                
            }

            public void StartListening()
            {
                Events.StartListening(this.cloudProxy.identity);
                this.receiveMessageTask = Option.Some(this.SetupMessageListening());
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
                                Events.MessageReceived(this.cloudProxy.identity);
                                IMessageConverter<Message> converter = this.cloudProxy.messageConverterProvider.Get<Message>();
                                IMessage message = converter.ToMessage(clientMessage);
                                // TODO: check if message delivery count exceeds the limit?
                                await this.cloudListener.ProcessMessageAsync(message);
                            }
                        }
                        catch (Exception e)
                        {
                            if(e is UnauthorizedException)
                            {
                                throw;
                            }
                            // continue when the client times out
                            // TODO: should limit the timeout?
                            Events.ErrorReceivingMessage(this.cloudProxy.identity, e);
                        }
                    }
                    Events.ReceiverStopped(this.cloudProxy.identity);
                }
                catch(Exception ex)
                {
                    Events.TerminatingErrorReceivingMessage(this.cloudProxy.identity, ex);
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
                Events.Closing(this.cloudProxy.identity);
                this.cancellationTokenSource.Cancel();
                return this.receiveMessageTask.GetOrElse(Task.CompletedTask);
            }

            public Task SetupCallMethodAsync()
            {
                return this.cloudProxy.deviceClient.SetMethodDefaultHandlerAsync(this.MethodCallHandler, null);
            }

            public Task RemoveCallMethodAsync()
            {
                return this.cloudProxy.deviceClient.SetMethodDefaultHandlerAsync(null, null);
            }

            internal async Task<MethodResponse> MethodCallHandler(MethodRequest methodrequest, object usercontext)
            {
                Preconditions.CheckNotNull(methodrequest, nameof(methodrequest));

                Events.MethodCallReceived(this.cloudProxy.identity);

                var direceMethodRequest = new DirectMethodRequest(this.cloudProxy.identity.Id, methodrequest.Name, methodrequest.Data, DeviceMethodMaxResponseTimeout);
                DirectMethodResponse directMethodResponse = await this.cloudListener.CallMethodAsync(direceMethodRequest);
                MethodResponse methodResponse = directMethodResponse.Data == null ? new MethodResponse(directMethodResponse.Status) : new MethodResponse(directMethodResponse.Data, directMethodResponse.Status);
                return methodResponse;
            }

            public Task SetupDesiredPropertyUpdatesAsync()
            {
                return this.cloudProxy.deviceClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyUpdates, this.desiredUpdateHandler);
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
                MethodResponseReceived,
                MethodRequestIdNotMatched,
                MethodResponseTimedout,
                StartListening
            }

            public static void Closed(CloudProxy cloudProxy)
            {
                Log.LogInformation((int)EventIds.Close, Invariant($"Closed cloud proxy for device {cloudProxy.identity.Id}"));
            }

            public static void ErrorClosing(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogError((int)EventIds.CloseError, ex, Invariant($"Error closing cloud proxy for device {cloudProxy.identity.Id}"));
            }

            public static void GetTwin(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.GetTwin, Invariant($"Getting twin for device {cloudProxy.identity.Id}"));
            }

            public static void SendMessage(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.SendMessage, Invariant($"Sending message for device {cloudProxy.identity.Id}"));
            }

            public static void ErrorSendingMessage(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogDebug((int)EventIds.SendMessageError, ex, Invariant($"Error sending message for device {cloudProxy.identity.Id}"));
            }

            public static void ErrorSendingBatchMessage(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogDebug((int)EventIds.SendMessageBatchError, ex, Invariant($"Error sending message batch for device {cloudProxy.identity.Id}"));
            }

            public static void UpdateReportedProperties(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.UpdateReportedProperties, Invariant($"Updating reported properties for device {cloudProxy.identity.Id}"));
            }

            public static void BindCloudListener(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.BindCloudListener, Invariant($"Binding cloud listener for device {cloudProxy.identity.Id}"));
            }

            public static void SendFeedbackMessage(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.SendFeedbackMessage, Invariant($"Sending feedback message for device {cloudProxy.identity.Id}"));
            }

            internal static void ExceptionInHandleException(Exception handlingException, Exception caughtException)
            {
                Log.LogDebug((int)EventIds.ExceptionInHandleException, Invariant($"Got exception {caughtException} while handling exception {handlingException}"));
            }

            public static void MessageReceived(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.MessageReceived, Invariant($"Received message from cloud for device {identity.Id}"));
            }

            public static void Closing(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.ClosingReceiver, Invariant($"Closing receiver for device {identity.Id}"));
            }

            public static void ErrorReceivingMessage(IIdentity identity, Exception ex)
            {
                Log.LogError((int)EventIds.ReceiveError, ex, Invariant($"Error receiving message for device {identity.Id}"));
            }

            public static void ReceiverStopped(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.ReceiverStopped, Invariant($"Cloud message receiver stopped for device {identity.Id}"));
            }

            public static void MethodCallReceived(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.MethodReceived, Invariant($"Received call method from cloud for device {identity.Id}"));
            }
            
            public static void StartListening(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.StartListening, Invariant($"Start listening for C2D messages for device {identity.Id}"));
            }

            internal static void TerminatingErrorReceivingMessage(IIdentity identity, Exception e)
            {
                Log.LogInformation((int)EventIds.ReceiveError, e, Invariant($"Error receiving C2D messages for device {identity.Id}. Closing receive loop."));
            }
        }
    }
}
