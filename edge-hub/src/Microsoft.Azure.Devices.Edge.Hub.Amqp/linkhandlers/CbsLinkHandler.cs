// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Address matches the template "/$cbs/*"
    /// </summary>
    public class CbsLinkHandler : LinkHandler
    {
        CbsLinkHandler(IAmqpLink link, Uri requestUri, IDictionary<string, string> boundVariables,
            IMessageConverter<AmqpMessage> messageConverter, IConnectionProvider connectionProvider)
            : base(link, requestUri, boundVariables, messageConverter, connectionProvider)
        {
        }

        public static ILinkHandler Create(IAmqpLink amqpLink, Uri requestUri,
            IDictionary<string, string> boundVariables, IMessageConverter<AmqpMessage> messageConverter,
            IConnectionProvider connectionProvider)
        {
            var cbsNode = amqpLink.Session.Connection.FindExtension<ICbsNode>();
            if (cbsNode == null)
            {
                throw new InvalidOperationException("CbsNode not found in the AMQP connection");
            }

            cbsNode.RegisterLink(amqpLink);
            LinkHandler cbsLinkHandler = new CbsLinkHandler(amqpLink, requestUri, boundVariables, messageConverter, connectionProvider);
            Events.Created(amqpLink);
            return cbsLinkHandler;
        }

        protected override string Name => "Cbs";

        protected override Task OnOpenAsync(TimeSpan timeout) => Task.CompletedTask;

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CbsLinkHandler>();
            const int IdStart = AmqpEventIds.CbsLinkHandler;

            enum EventIds
            {
                Created = IdStart
            }

            public static void Created(IAmqpLink amqpLink)
            {
                Log.LogDebug((int)EventIds.Created, "New CBS {0} link created".FormatInvariant(amqpLink.IsReceiver ? "receiver" : "sender"));
            }
        }
    }
}
