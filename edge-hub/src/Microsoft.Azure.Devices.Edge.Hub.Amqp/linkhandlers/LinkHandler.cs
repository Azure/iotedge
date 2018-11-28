// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public abstract class LinkHandler : ILinkHandler
    {
        readonly IConnectionHandler connectionHandler;

        protected LinkHandler(
            IIdentity identity,
            IAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IConnectionHandler connectionHandler,
            IMessageConverter<AmqpMessage> messageConverter)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.MessageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.BoundVariables = Preconditions.CheckNotNull(boundVariables, nameof(boundVariables));
            this.Link = Preconditions.CheckNotNull(link, nameof(link));
            this.LinkUri = Preconditions.CheckNotNull(requestUri, nameof(requestUri));
            this.Link.SafeAddClosed(this.OnLinkClosed);
            this.connectionHandler = Preconditions.CheckNotNull(connectionHandler, nameof(connectionHandler));
        }

        protected IIdentity Identity { get; }

        protected string ClientId => this.Identity.Id;

        protected IMessageConverter<AmqpMessage> MessageConverter { get; }

        protected IDeviceListener DeviceListener { get; private set; }

        protected IDictionary<string, string> BoundVariables { get; }

        public IAmqpLink Link { get; }

        public Uri LinkUri { get; }

        public abstract LinkType Type { get; }

        public virtual string CorrelationId { get; } = Guid.NewGuid().ToString();

        public async Task OpenAsync(TimeSpan timeout)
        {
            if (!await this.Authenticate())
            {
                throw new InvalidOperationException($"Unable to open {this.Type} link as connection is not authenticated");
            }

            this.DeviceListener = await this.connectionHandler.GetDeviceListener();

            await this.OnOpenAsync(timeout);
            await this.connectionHandler.RegisterLinkHandler(this);
            Events.Opened(this);
        }

        protected abstract Task OnOpenAsync(TimeSpan timeout);

        protected Task<bool> Authenticate()
        {
            IAmqpAuthenticator amqpAuth;
            IAmqpConnection connection = this.Link.Session.Connection;

            // Check if Principal is SaslPrincipal
            if (connection.Principal is SaslPrincipal saslPrincipal)
            {
                amqpAuth = saslPrincipal;
            }
            else
            {
                // Else the connection uses CBS authentication. Get AmqpAuthentication from the CbsNode                    
                var cbsNode = connection.FindExtension<ICbsNode>();
                amqpAuth = cbsNode ?? throw new InvalidOperationException("CbsNode is null");
            }

            return amqpAuth.AuthenticateAsync(this.Identity.Id);
        }

        protected virtual void OnLinkClosed(object sender, EventArgs args)
        {
            Events.Closed(this);
            this.connectionHandler.RemoveLinkHandler(this);
        }

        public async Task CloseAsync(TimeSpan timeout)
        {
            await this.Link.CloseAsync(timeout);
            Events.Closed(this);
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<LinkHandler>();
            const int IdStart = AmqpEventIds.LinkHandler;

            enum EventIds
            {
                Closing = IdStart,
                Opened
            }

            public static void Closed(LinkHandler handler)
            {
                Log.LogInformation((int)EventIds.Closing, $"Closing link {handler.Type} for {handler.ClientId}");
            }

            public static void Opened(LinkHandler handler)
            {
                Log.LogInformation((int)EventIds.Opened, $"Opened link {handler.Type} for {handler.ClientId}");
            }
        }
    }
}
