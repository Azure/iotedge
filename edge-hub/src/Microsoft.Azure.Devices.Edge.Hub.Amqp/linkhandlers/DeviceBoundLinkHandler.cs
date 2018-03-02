// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    /// <summary>
    /// Address matches the template "/devices/{0}/messages/deviceBound"
    /// </summary>
    public class DeviceBoundLinkHandler : LinkHandler
    {
        DeviceBoundLinkHandler(IAmqpLink link, Uri requestUri, IDictionary<string, string> boundVariables,
            IMessageConverter<AmqpMessage> messageConverter, IConnectionProvider connectionProvider)
            : base(link, requestUri, boundVariables, messageConverter, connectionProvider)
        {
        }

        public static ILinkHandler Create(IAmqpLink link, Uri requestUri,
            IDictionary<string, string> boundVariables, IMessageConverter<AmqpMessage> messageConverter, IConnectionProvider connectionProvider)
        {
            if (link.IsReceiver)
            {
                throw new InvalidOperationException("DeviceBoundLink cannot be receiver");
            }

            return new DeviceBoundLinkHandler(link, requestUri, boundVariables, messageConverter, connectionProvider);
        }

        protected override string Name => "DeviceBound";

        protected override Task OnOpenAsync(TimeSpan timeout) => Task.CompletedTask;
    }
}
