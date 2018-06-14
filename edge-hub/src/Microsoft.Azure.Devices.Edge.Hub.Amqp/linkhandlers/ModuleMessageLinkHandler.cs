// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    /// <summary>
    /// This handler is used to send messages to modules
    /// Address matches the template "/devices/{deviceid}/modules/{moduleid}/messages/events"
    /// </summary>
    class ModuleMessageLinkHandler : SendingLinkHandler
    {
        public ModuleMessageLinkHandler(ISendingAmqpLink link, Uri requestUri, IDictionary<string, string> boundVariables,
            IMessageConverter<AmqpMessage> messageConverter)
            : base(link, requestUri, boundVariables, messageConverter)
        {
        }

        public override LinkType Type => LinkType.ModuleMessages;

        protected override QualityOfService QualityOfService => QualityOfService.AtLeastOnce;
    }
}
