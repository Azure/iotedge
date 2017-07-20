// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
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
        CloudReceiver cloudReceiver;

        public CloudProxy(DeviceClient deviceClient, IMessageConverterProvider messageConverterProvider, IIdentity identity)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.messageConverterProvider = Preconditions.CheckNotNull(messageConverterProvider, nameof(messageConverterProvider));
            this.isActive = new AtomicBoolean(true);
            this.identity = Preconditions.CheckNotNull(identity, nameof(identity));
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
                }
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

        public async Task<bool> SendMessageAsync(IMessage inputMessage)
        {
            Preconditions.CheckNotNull(inputMessage, nameof(inputMessage));
            IMessageConverter<Message> converter = this.messageConverterProvider.Get<Message>();
            Message message = converter.FromMessage(inputMessage);

            try
            {
                await this.deviceClient.SendEventAsync(message);
                Events.SendMessage(this);
                return true;
            }
            catch (Exception ex)
            {
                Events.ErrorSendingMessage(this, ex);
                return false;
            }
        }

        public async Task<bool> SendMessageBatchAsync(IEnumerable<IMessage> inputMessages)
        {
            IMessageConverter<Message> converter = this.messageConverterProvider.Get<Message>();
            IEnumerable<Message> messages = Preconditions.CheckNotNull(inputMessages, nameof(inputMessages))
                .Select(inputMessage => converter.FromMessage(inputMessage));
            try
            {
                await this.deviceClient.SendEventBatchAsync(messages);
                Events.SendMessage(this);
                return true;
            }
            catch (Exception ex)
            {
                Events.ErrorSendingBatchMessage(this, ex);
                return false;
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
            this.cloudReceiver = new CloudReceiver(this.deviceClient, this.messageConverterProvider, cloudListener, this.identity);
            Events.BindCloudListener(this);
        }

        public bool IsActive => this.isActive.Get();

        public Task SendFeedbackMessageAsync(IFeedbackMessage message)
        {
            Preconditions.CheckNotNull(message, nameof(message));
            message.SystemProperties.TryGetValue(SystemProperties.MessageId, out string messageId);
            Events.SendFeedbackMessage(this);
            switch (message.FeedbackStatus)
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
                SendFeedbackMessage
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
                Log.LogError((int)EventIds.SendMessageError, ex, Invariant($"Error sending message for device {cloudProxy.identity.Id}"));
            }

            public static void ErrorSendingBatchMessage(CloudProxy cloudProxy, Exception ex)
            {
                Log.LogError((int)EventIds.SendMessageBatchError, ex, Invariant($"Error sending message batch for device {cloudProxy.identity.Id}"));
            }

            public static void UpdateReportedProperties(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.UpdateReportedProperties, Invariant($"Updating resported properties for device {cloudProxy.identity.Id}"));
            }

            public static void BindCloudListener(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.BindCloudListener, Invariant($"Binding cloud listener for device {cloudProxy.identity.Id}"));
            }

            public static void SendFeedbackMessage(CloudProxy cloudProxy)
            {
                Log.LogDebug((int)EventIds.SendFeedbackMessage, Invariant($"Sending feedback message for device {cloudProxy.identity.Id}"));
            }
        }        
    }
}
