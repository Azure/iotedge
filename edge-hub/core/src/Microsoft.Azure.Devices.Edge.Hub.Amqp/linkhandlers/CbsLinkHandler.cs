// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Address matches the template "/$cbs/*"
    /// </summary>
    public class CbsLinkHandler : ILinkHandler
    {
        CbsLinkHandler(IAmqpLink link, Uri linkUri)
        {
            this.LinkUri = Preconditions.CheckNotNull(linkUri, nameof(linkUri));
            this.Link = Preconditions.CheckNotNull(link, nameof(link));
        }

        public Uri LinkUri { get; }

        public IAmqpLink Link { get; }

        public LinkType Type => LinkType.Cbs;

        public string CorrelationId { get; } = Guid.NewGuid().ToString();

        public static ILinkHandler Create(IAmqpLink amqpLink, Uri requestUri)
        {
            var cbsNode = amqpLink.Session.Connection.FindExtension<ICbsNode>();
            if (cbsNode == null)
            {
                throw new InvalidOperationException("CbsNode not found in the AMQP connection");
            }

            cbsNode.RegisterLink(amqpLink);
            ILinkHandler cbsLinkHandler = new CbsLinkHandler(amqpLink, requestUri);
            Events.Created(amqpLink);
            return cbsLinkHandler;
        }

        public Task CloseAsync(TimeSpan timeout)
        {
            Events.Closing(this.Link);
            return Task.CompletedTask;
        }

        public Task OpenAsync(TimeSpan timeout) => Task.CompletedTask;

        static class Events
        {
            const int IdStart = AmqpEventIds.CbsLinkHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<CbsLinkHandler>();

            enum EventIds
            {
                Closing = IdStart
            }

            public static void Created(IAmqpLink amqpLink)
            {
                Log.LogDebug((int)EventIds.Closing, "New CBS {0} link created".FormatInvariant(amqpLink.IsReceiver ? "receiver" : "sender"));
            }

            public static void Closing(IAmqpLink amqpLink)
            {
                Log.LogDebug((int)EventIds.Closing, "Closing CBS {0} link".FormatInvariant(amqpLink.IsReceiver ? "receiver" : "sender"));
            }
        }
    }
}
