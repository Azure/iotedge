// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;

    /// <summary>
    /// This handler is used to send messages to modules
    /// Address matches the template "/devices/{deviceid}/modules/{moduleid}/messages/events"
    /// </summary>
    class ModuleMessageLinkHandler : SendingLinkHandler
    {
        public ModuleMessageLinkHandler(
            IIdentity identity,
            ISendingAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IConnectionHandler connectionHandler,
            IMessageConverter<AmqpMessage> messageConverter,
            IProductInfoStore productInfoStore)
            : base(identity, link, requestUri, boundVariables, connectionHandler, messageConverter, productInfoStore)
        {
        }

        public override LinkType Type => LinkType.ModuleMessages;

        protected override QualityOfService QualityOfService => QualityOfService.AtLeastOnce;

        protected override async Task OnOpenAsync(TimeSpan timeout)
        {
            await base.OnOpenAsync(timeout);
            await this.DeviceListener.AddSubscription(DeviceSubscription.ModuleMessages);
        }

        protected override void OnMessageSent()
        {
            Metrics.AddSentMessage(this.Identity);
        }

        static class Metrics
        {
            static readonly IMetricsCounter SentMessagesCounter = Util.Metrics.Metrics.Instance.CreateCounter(
                "edgehub_messages_sent_total",
                new Dictionary<string, string>
                {
                    ["Protocol"] = "Amqp"
                });

            public static void AddSentMessage(IIdentity identity)
            {
                SentMessagesCounter.Increment(1, new Dictionary<string, string>
                {
                    ["Id"] = identity.Id
                });
            }
        }
    }
}
