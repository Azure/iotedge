// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Address matches the template "/devices/{0}/messages/deviceBound"
    /// </summary>
    public class DeviceBoundLinkHandler : LinkHandler
    {
        readonly ISendingAmqpLink sendingAmqpLink;
        readonly string deviceId;

        DeviceBoundLinkHandler(IAmqpLink link, Uri requestUri, IDictionary<string, string> boundVariables,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(link, requestUri, boundVariables, messageConverter)
        {
            this.sendingAmqpLink = (ISendingAmqpLink)link;
            this.deviceId = this.BoundVariables[Templates.DeviceIdTemplateParameterName];
        }

        public static ILinkHandler Create(IAmqpLink link, Uri requestUri,
            IDictionary<string, string> boundVariables, IMessageConverter<AmqpMessage> messageConverter)
        {
            if (link.IsReceiver)
            {
                throw new InvalidOperationException("DeviceBoundLink cannot be receiver");
            }

            ILinkHandler linkHandler = new DeviceBoundLinkHandler(link, requestUri, boundVariables, messageConverter);
            Events.Created();
            return linkHandler;
        }

        protected override string Name => "DeviceBound";

        protected override Task OnOpenAsync(TimeSpan timeout)
        {
            try
            {
                // TODO: Check if we need to worry about credit available on the link

                // SenderSettleMode.Unsettled (null as it is the default and to avoid bytes on the wire)
                this.sendingAmqpLink.Settings.SndSettleMode = null;
                // The receiver will only settle after sending the disposition to the sender and receiving a disposition indicating settlement of the delivery from the sender.
                this.sendingAmqpLink.Settings.RcvSettleMode = (byte)ReceiverSettleMode.Second;
                this.sendingAmqpLink.RegisterDispositionListener(this.DeviceDispositionListener);

                this.ConnectionHandler.RegisterC2DMessageSender(this.OnMessage);
                this.DeviceListener.StartListeningToC2DMessages();
                Events.Opened(this);
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                Events.ErrorOpeningLink(e, this);
                throw;
            }
        }

        Task OnMessage(IMessage message)
        {
            AmqpMessage amqpMessage = this.MessageConverter.FromMessage(message);

            amqpMessage.Properties.To =
                this.Identity is IModuleIdentity moduleIdentity
                ? $"/devices/{HttpUtility.UrlEncode(moduleIdentity.DeviceId)}/modules/{HttpUtility.UrlEncode(moduleIdentity.ModuleId)} "
                : $"/devices/{HttpUtility.UrlEncode(this.Identity.Id)}";

            amqpMessage.DeliveryTag = message.SystemProperties.TryGetNonEmptyValue(SystemProperties.LockToken, out string lockToken)
                && Guid.TryParse(lockToken, out Guid lockTokenGuid)
                ? new ArraySegment<byte>(lockTokenGuid.ToByteArray())
                : new ArraySegment<byte>(Guid.NewGuid().ToByteArray());

            // Use the sync variant here as the async call cannot be used when
            // a disposition listener has been registered. 
            this.sendingAmqpLink.SendMessageNoWait(amqpMessage, amqpMessage.DeliveryTag, AmqpConstants.NullBinary);
            Events.MessageSent(this.Identity, lockToken);
            return Task.CompletedTask;
        }

        void DeviceDispositionListener(Delivery delivery) => this.DisposeMessageAsync(delivery);

        async void DisposeMessageAsync(Delivery delivery)
        {
            try
            {
                Preconditions.CheckNotNull(delivery, nameof(delivery));
                Preconditions.CheckArgument(delivery.State != null, "Delivery.State should not be null");

                string lockToken = new Guid(delivery.DeliveryTag.Array).ToString();
                FeedbackStatus feedbackStatus = GetFeedbackStatus(delivery);
                await this.DeviceListener.ProcessMessageFeedbackAsync(lockToken, feedbackStatus);
                this.sendingAmqpLink.DisposeDelivery(delivery, true, new Accepted());
            }
            catch (Exception ex)
            {
                Events.ErrorDisposingMessage(this.Identity, ex);
            }
        }

        internal static FeedbackStatus GetFeedbackStatus(Delivery delivery)
        {
            if (delivery.State.DescriptorCode == AmqpConstants.AcceptedOutcome.DescriptorCode)
            {
                return FeedbackStatus.Complete;
            }
            else if (delivery.State.DescriptorCode == AmqpConstants.RejectedOutcome.DescriptorCode)
            {
                return FeedbackStatus.Reject;
            }
            else if (delivery.State.DescriptorCode == AmqpConstants.ReleasedOutcome.DescriptorCode)
            {
                return FeedbackStatus.Abandon;
            }
            else
            {
                throw new InvalidOperationException($"Unknown disposition outcome - {delivery.State.DescriptorCode}");
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceBoundLinkHandler>();
            const int IdStart = AmqpEventIds.DeviceBoundLinkHandler;

            enum EventIds
            {
                Created = IdStart,
                ErrorDisposing,
                Opened,
                MessageSent
            }

            public static void Created()
            {
                Log.LogDebug((int)EventIds.Created, "New device bound link created");
            }

            public static void ErrorDisposingMessage(IIdentity identity, Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorDisposing, ex, $"Error disposing message for {identity.Id}");
            }

            public static void ErrorOpeningLink(Exception ex, DeviceBoundLinkHandler deviceBoundLinkHandler)
            {
                Log.LogWarning((int)EventIds.ErrorDisposing, ex, $"Error disposing message for {deviceBoundLinkHandler.deviceId ?? string.Empty}");
            }

            public static void Opened(DeviceBoundLinkHandler deviceBoundLinkHandler)
            {
                Log.LogDebug((int)EventIds.Opened, $"Opened device bound link handler for {deviceBoundLinkHandler.deviceId}");
            }

            public static void MessageSent(IIdentity identity, string messageId)
            {
                Log.LogDebug((int)EventIds.MessageSent, $"Sent C2D message with id {messageId} to device {identity.Id}");
            }
        }
    }
}
