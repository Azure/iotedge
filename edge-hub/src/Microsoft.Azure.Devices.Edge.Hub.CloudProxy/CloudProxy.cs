// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
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
        readonly IMessageConverter<Message> messageConverter;
        readonly IMessageConverter<Twin> twinConverter;
        readonly AtomicBoolean isActive;
        CloudReceiver cloudReceiver;

        public CloudProxy(DeviceClient deviceClient, IMessageConverter<Message> messageConverter, IMessageConverter<Twin> twinConverter, IIdentity identity)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.twinConverter = Preconditions.CheckNotNull(twinConverter, nameof(twinConverter));
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
                        await this.RemoveCallMethodAsync();
                    }
                    // remove direct method subscription
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
            return this.twinConverter.ToMessage(twin);
        }

        public async Task<bool> SendMessageAsync(IMessage inputMessage)
        {
            Preconditions.CheckNotNull(inputMessage, nameof(inputMessage));
            Message message = this.messageConverter.FromMessage(inputMessage);

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
            IEnumerable<Message> messages = Preconditions.CheckNotNull(inputMessages, nameof(inputMessages))
                .Select(inputMessage => this.messageConverter.FromMessage(inputMessage));
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

        public async Task UpdateReportedPropertiesAsync(string reportedProperties)
        {
            var reported = JsonConvert.DeserializeObject<TwinCollection>(reportedProperties);
            await this.deviceClient.UpdateReportedPropertiesAsync(reported);
            Events.UpdateReportedProperties(this);
        }

        public void BindCloudListener(ICloudListener cloudListener)
        {
            this.cloudReceiver = new CloudReceiver(this.deviceClient, this.messageConverter, cloudListener, this.identity);
            this.cloudReceiver.StartListening();
            Events.BindCloudListener(this);
        }

        public bool IsActive => this.isActive.Get();

        public Task SendFeedbackMessageAsync(IFeedbackMessage message)
        {
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

        public Task SetupCallMethodAsync()
        {
            return this.cloudReceiver.SetupCallMethodAsync();
        }

        public Task RemoveCallMethodAsync()
        {
            return this.cloudReceiver.RemoveCallMethodAsync();
        }

        public Task SetupDesiredPropertyUpdatesAsync()
        {
            return this.cloudReceiver.SetupDesiredPropertyUpdatesAsync();
        }

        public Task RemoveDesiredPropertyUpdatesAsync()
        {
            return this.cloudReceiver.RemoveDesiredPropertyUpdatesAsync();
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
