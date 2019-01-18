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
        readonly IClient client;
        readonly IMessageConverterProvider messageConverterProvider;
        readonly Action<string, CloudConnectionStatus> connectionStatusChangedHandler;
        readonly string clientId;
        readonly Guid id = Guid.NewGuid();
        readonly CloudReceiver cloudReceiver;
        readonly ResettableTimer timer;
        readonly bool closeOnIdleTimeout;

        public CloudProxy(
            IClient client,
            IMessageConverterProvider messageConverterProvider,
            string clientId,
            Action<string, CloudConnectionStatus> connectionStatusChangedHandler,
            ICloudListener cloudListener,
            TimeSpan idleTimeout,
            bool closeOnIdleTimeout)
        {
            this.client = Preconditions.CheckNotNull(client, nameof(client));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.clientId = Preconditions.CheckNonWhiteSpace(clientId, nameof(clientId));
            this.cloudReceiver = new CloudReceiver(this, Preconditions.CheckNotNull(cloudListener, nameof(cloudListener)));
            this.timer = new ResettableTimer(this.HandleIdleTimeout, idleTimeout, Events.Log, true);
            this.timer.Start();
            this.closeOnIdleTimeout = closeOnIdleTimeout;
            if (connectionStatusChangedHandler != null)
            {
                this.connectionStatusChangedHandler = connectionStatusChangedHandler;
            }

            Events.Initialized(this);
        }

        public bool IsActive => this.client.IsActive;

        public async Task<bool> CloseAsync()
        {
            try
            {
                // In offline scenario, sometimes the underlying connection has already closed and
                // in that case calling CloseAsync throws. This needs to be fixed in the SDK, but meanwhile
                // wrapping this.client.CloseAsync in a try/catch
                try
                {
                    await this.client.CloseAsync();
                }
                catch (Exception ex)
                {
                    Events.ErrorClosingClient(this.clientId, ex);
                }

                await (this.cloudReceiver?.CloseAsync() ?? Task.CompletedTask);
                this.timer.Disable();
                Events.Closed(this);
                return true;
            }
            catch (Exception ex)
            {
                Events.ErrorClosing(this, ex);
                return false;
            }
        }

        public async Task<bool> OpenAsync()
        {
            try
            {
                await this.client.OpenAsync();
                this.timer.Reset();
                return true;
            }
            catch (Exception ex)
            {
                Events.ErrorOpening(this.clientId, ex);
                await this.HandleException(ex);
                throw;
            }
        }

        public async Task<IMessage> GetTwinAsync()
        {
            this.timer.Reset();
            try
            {
                Twin twin = await this.client.GetTwinAsync();
                Events.GetTwin(this);
                IMessageConverter<Twin> converter = this.messageConverterProvider.Get<Twin>();
                return converter.ToMessage(twin);
            }
            catch (Exception ex)
            {
                Events.ErrorGettingTwin(this, ex);
                await this.HandleException(ex);
                throw;
            }
        }

        public async Task SendMessageAsync(IMessage inputMessage)
        {
            Preconditions.CheckNotNull(inputMessage, nameof(inputMessage));
            IMessageConverter<Message> converter = this.messageConverterProvider.Get<Message>();
            Message message = converter.FromMessage(inputMessage);
            this.timer.Reset();
            try
            {
                await this.client.SendEventAsync(message);
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
            this.timer.Reset();
            try
            {
                await this.client.SendEventBatchAsync(messages);
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
            TwinCollection reported = JsonConvert.DeserializeObject<TwinCollection>(reportedPropertiesString);
            this.timer.Reset();
            try
            {
                await this.client.UpdateReportedPropertiesAsync(reported);
                Events.UpdateReportedProperties(this);
            }
            catch (Exception e)
            {
                Events.ErrorUpdatingReportedProperties(this, e);
                await this.HandleException(e);
                throw;
            }
        }

        public async Task SendFeedbackMessageAsync(string messageId, FeedbackStatus feedbackStatus)
        {
            Preconditions.CheckNonWhiteSpace(messageId, nameof(messageId));
            Events.SendFeedbackMessage(this);
            this.timer.Reset();
            try
            {
                switch (feedbackStatus)
                {
                    case FeedbackStatus.Complete:
                        await this.client.CompleteAsync(messageId);
                        return;

                    case FeedbackStatus.Abandon:
                        await this.client.AbandonAsync(messageId);
                        return;

                    case FeedbackStatus.Reject:
                        await this.client.RejectAsync(messageId);
                        return;

                    default:
                        throw new InvalidOperationException("Feedback status type is not supported");
                }
            }
            catch (Exception e)
            {
                Events.ErrorSendingFeedbackMessageAsync(this, e);
                await this.HandleException(e);
                throw;
            }
        }

        public Task SetupCallMethodAsync() =>
            this.EnsureCloudReceiver(nameof(this.SetupCallMethodAsync)) ? this.cloudReceiver.SetupCallMethodAsync() : Task.CompletedTask;

        public Task RemoveCallMethodAsync() =>
            this.EnsureCloudReceiver(nameof(this.RemoveCallMethodAsync)) ? this.cloudReceiver.RemoveCallMethodAsync() : Task.CompletedTask;

        public Task SetupDesiredPropertyUpdatesAsync() =>
            this.EnsureCloudReceiver(nameof(this.SetupDesiredPropertyUpdatesAsync)) ? this.cloudReceiver.SetupDesiredPropertyUpdatesAsync() : Task.CompletedTask;

        public Task RemoveDesiredPropertyUpdatesAsync() =>
            this.EnsureCloudReceiver(nameof(this.RemoveDesiredPropertyUpdatesAsync)) ? this.cloudReceiver.RemoveDesiredPropertyUpdatesAsync() : Task.CompletedTask;

        public void StartListening()
        {
            if (this.EnsureCloudReceiver(nameof(this.RemoveDesiredPropertyUpdatesAsync)))
            {
                this.cloudReceiver.StartListening();
            }
        }

        // This API is to be used for Tests only.
        internal CloudReceiver GetCloudReceiver() => this.cloudReceiver;

        Task HandleIdleTimeout()
        {
            if (this.closeOnIdleTimeout)
            {
                Events.TimedOutClosing(this);
                return this.CloseAsync();
            }

            return Task.CompletedTask;
        }

        bool EnsureCloudReceiver(string operation)
        {
            if (this.cloudReceiver == null)
            {
                Events.CloudReceiverNull(this.clientId, operation);
                return false;
            }

            this.timer.Disable();
            return true;
        }

        Task HandleException(Exception ex)
        {
            try
            {
                if (ex is UnauthorizedException)
                {
                    this.connectionStatusChangedHandler(this.clientId, CloudConnectionStatus.DisconnectedTokenExpired);
                }
                else if (ex is NullReferenceException)
                {
                    Events.HandleNre(this);
                    return this.CloseAsync();
                }
            }
            catch (Exception e)
            {
                Events.ExceptionInHandleException(this, ex, e);
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
            static readonly TimeSpan ReceiveC2DMessageTimeout = TimeSpan.FromSeconds(20);

            public CloudReceiver(CloudProxy cloudProxy, ICloudListener cloudListener)
            {
                this.cloudProxy = Preconditions.CheckNotNull(cloudProxy, nameof(cloudProxy));
                this.cloudListener = Preconditions.CheckNotNull(cloudListener, nameof(cloudListener));
                IMessageConverter<TwinCollection> converter = cloudProxy.messageConverterProvider.Get<TwinCollection>();
                this.desiredUpdateHandler = new DesiredPropertyUpdateHandler(cloudListener, converter, cloudProxy);
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
                            this.receiveMessageTask = Option.Some(this.C2DMessagesLoop(this.cloudProxy.client));
                        }
                    }
                }
            }

            public Task CloseAsync()
            {
                Events.Closing(this.cloudProxy);
                this.cancellationTokenSource.Cancel();
                return this.receiveMessageTask.GetOrElse(Task.CompletedTask);
            }

            public Task SetupCallMethodAsync() => this.cloudProxy.client.SetMethodDefaultHandlerAsync(this.MethodCallHandler, null);

            public Task RemoveCallMethodAsync() => this.cloudProxy.client.SetMethodDefaultHandlerAsync(null, null);

            public Task SetupDesiredPropertyUpdatesAsync() => this.cloudProxy.client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyUpdates, this.desiredUpdateHandler);

            public Task RemoveDesiredPropertyUpdatesAsync()
            {
                // return this.deviceClient.SetDesiredPropertyUpdateCallback(null, null);
                // TODO: update device SDK to unregister callback for desired properties by passing null
                return Task.CompletedTask;
            }

            internal async Task<MethodResponse> MethodCallHandler(MethodRequest methodrequest, object usercontext)
            {
                Preconditions.CheckNotNull(methodrequest, nameof(methodrequest));

                Events.MethodCallReceived(this.cloudProxy.clientId);
                var direceMethodRequest = new DirectMethodRequest(this.cloudProxy.clientId, methodrequest.Name, methodrequest.Data, DeviceMethodMaxResponseTimeout);
                DirectMethodResponse directMethodResponse = await this.cloudListener.CallMethodAsync(direceMethodRequest);
                MethodResponse methodResponse = directMethodResponse.Data == null ? new MethodResponse(directMethodResponse.Status) : new MethodResponse(directMethodResponse.Data, directMethodResponse.Status);
                return methodResponse;
            }

            static Task OnDesiredPropertyUpdates(TwinCollection desiredProperties, object userContext)
            {
                var handler = (DesiredPropertyUpdateHandler)userContext;
                return handler.OnDesiredPropertyUpdates(desiredProperties);
            }

            async Task C2DMessagesLoop(IClient deviceClient)
            {
                Message clientMessage = null;
                try
                {
                    while (!this.cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        try
                        {
                            clientMessage = await deviceClient.ReceiveAsync(ReceiveC2DMessageTimeout);
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
                            if (e is UnauthorizedException || e is ObjectDisposedException)
                            {
                                throw;
                            }

                            // Wait for some time before trying again.
                            await Task.Delay(ReceiveC2DMessageTimeout);
                            Events.ErrorReceivingMessage(this.cloudProxy, e);
                        }
                    }

                    Events.ReceiverStopped(this.cloudProxy);
                }
                catch (Exception ex)
                {
                    Events.TerminatingErrorReceivingMessage(this.cloudProxy, ex);
                    this.receiveMessageTask = Option.None<Task>();
                    await this.cloudProxy.HandleException(ex);
                }
                finally
                {
                    clientMessage?.Dispose();
                }
            }

            class DesiredPropertyUpdateHandler
            {
                readonly ICloudListener listener;
                readonly IMessageConverter<TwinCollection> converter;
                readonly CloudProxy cloudProxy;

                public DesiredPropertyUpdateHandler(
                    ICloudListener listener,
                    IMessageConverter<TwinCollection> converter,
                    CloudProxy cloudProxy)
                {
                    this.listener = listener;
                    this.converter = converter;
                    this.cloudProxy = cloudProxy;
                }

                public Task OnDesiredPropertyUpdates(TwinCollection desiredProperties)
                {
                    return this.listener.OnDesiredPropertyUpdates(this.converter.ToMessage(desiredProperties));
                }
            }
        }

        static class Events
        {
            public static readonly ILogger Log = Logger.Factory.CreateLogger<CloudProxy>();
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
                StartListening,
                CloudReceiverNull,
                ErrorOpening,
                TimedOutClosing,
                Initialized,
                ErrorClosingClient,
                ErrorUpdatingReportedProperties,
                ErrorSendingFeedbackMessageAsync,
                ErrorGettingTwin,
                HandleNre
            }

            public static void Closed(CloudProxy cloudProxy)
            {
                Log.LogInformation((int)EventIds.Close, Invariant($"Closed cloud proxy {cloudProxy.id} for {cloudProxy.clientId}"));
            }

            public static void ErrorClosing(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogError((int)EventIds.CloseError, ex, Invariant($"Error closing cloud proxy {cloudProxy.id} for device {cloudProxy.clientId}"));
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
                Log.LogDebug((int)EventIds.SendMessageError, ex, Invariant($"Error sending message for device {cloudProxy.clientId} in cloud proxy {cloudProxy.id}"));
            }

            public static void ErrorSendingBatchMessage(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogDebug((int)EventIds.SendMessageBatchError, ex, Invariant($"Error sending message batch for device {cloudProxy.clientId} in cloud proxy {cloudProxy.id}"));
            }

            public static void UpdateReportedProperties(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.UpdateReportedProperties, Invariant($"Updating reported properties for device {cloudProxy.clientId}"));
            }

            public static void SendFeedbackMessage(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.SendFeedbackMessage, Invariant($"Sending feedback message for device {cloudProxy.clientId}"));
            }

            public static void MessageReceived(string clientId)
            {
                Log.LogDebug((int)EventIds.MessageReceived, Invariant($"Received message from cloud for device {clientId}"));
            }

            public static void Closing(CloudProxy cloudProxy)
            {
                Log.LogInformation((int)EventIds.ClosingReceiver, Invariant($"Closing receiver in cloud proxy {cloudProxy.id} for {cloudProxy.clientId}"));
            }

            public static void ErrorReceivingMessage(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogDebug((int)EventIds.ReceiveError, ex, Invariant($"Error receiving message for device {cloudProxy.clientId} in cloud proxy {cloudProxy.id}"));
            }

            public static void ReceiverStopped(CloudProxy cloudProxy)
            {
                Log.LogInformation((int)EventIds.ReceiverStopped, Invariant($"Cloud message receiver stopped for device {cloudProxy.clientId} in cloud proxy {cloudProxy.id}"));
            }

            public static void MethodCallReceived(string clientId)
            {
                Log.LogDebug((int)EventIds.MethodReceived, Invariant($"Received call method from cloud for device {clientId}"));
            }

            public static void StartListening(string clientId)
            {
                Log.LogInformation((int)EventIds.StartListening, Invariant($"Start listening for C2D messages for device {clientId}"));
            }

            public static void ErrorOpening(string clientId, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorOpening, ex, Invariant($"Error opening IotHub connection for device {clientId}"));
            }

            public static void TimedOutClosing(CloudProxy cloudProxy)
            {
                Log.LogInformation((int)EventIds.TimedOutClosing, Invariant($"Closing cloud proxy {cloudProxy.id} for {cloudProxy.clientId} because of inactivity"));
            }

            public static void Initialized(CloudProxy cloudProxy)
            {
                Log.LogInformation((int)EventIds.Initialized, Invariant($"Initialized cloud proxy {cloudProxy.id} for {cloudProxy.clientId}"));
            }

            public static void ErrorClosingClient(string clientId, Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorClosingClient, ex, Invariant($"Error closing client for {clientId}"));
            }

            public static void ErrorUpdatingReportedProperties(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorUpdatingReportedProperties, ex, Invariant($"Error sending updated reported properties for {cloudProxy.clientId} in cloud proxy {cloudProxy.id}"));
            }

            public static void ErrorSendingFeedbackMessageAsync(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorSendingFeedbackMessageAsync, ex, Invariant($"Error sending feedback message for {cloudProxy.clientId} in cloud proxy {cloudProxy.id}"));
            }

            public static void ErrorGettingTwin(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorGettingTwin, ex, Invariant($"Error getting twin for {cloudProxy.clientId} in cloud proxy {cloudProxy.id}"));
            }

            public static void HandleNre(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.HandleNre, Invariant($"Got a NullReferenceException from client for {cloudProxy.clientId}. Closing the cloud proxy since it may be in a bad state."));
            }

            internal static void ExceptionInHandleException(CloudProxy cloudProxy, Exception handlingException, Exception caughtException)
            {
                Log.LogDebug((int)EventIds.ExceptionInHandleException, Invariant($"Cloud proxy {cloudProxy.id} got exception {caughtException} while handling exception {handlingException}"));
            }

            internal static void TerminatingErrorReceivingMessage(CloudProxy cloudProxy, Exception e)
            {
                Log.LogInformation((int)EventIds.ReceiveError, e, Invariant($"Error receiving C2D messages for device {cloudProxy.clientId} in cloud proxy {cloudProxy.id}. Closing receive loop."));
            }

            internal static void CloudReceiverNull(string clientId, string operation)
            {
                Log.LogWarning((int)EventIds.CloudReceiverNull, Invariant($"Cannot complete operation {operation} for device {clientId} because cloud receiver is null"));
            }
        }
    }
}
