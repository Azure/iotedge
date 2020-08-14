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
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public abstract class LinkHandler : ILinkHandler
    {
        readonly IConnectionHandler connectionHandler;
        readonly IMetadataStore metadataStore;

        protected LinkHandler(
            IIdentity identity,
            IAmqpLink link,
            Uri requestUri,
            IDictionary<string, string> boundVariables,
            IConnectionHandler connectionHandler,
            IMessageConverter<AmqpMessage> messageConverter,
            IMetadataStore metadataStore)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.MessageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.BoundVariables = Preconditions.CheckNotNull(boundVariables, nameof(boundVariables));
            this.Link = Preconditions.CheckNotNull(link, nameof(link));
            this.LinkUri = Preconditions.CheckNotNull(requestUri, nameof(requestUri));
            this.Link.SafeAddClosed(this.OnLinkClosed);
            this.connectionHandler = Preconditions.CheckNotNull(connectionHandler, nameof(connectionHandler));
            this.metadataStore = Preconditions.CheckNotNull(metadataStore, nameof(metadataStore));

            string clientVersion = null;
            if (this.Link.Settings?.Properties?.TryGetValue(IotHubAmqpProperty.ClientVersion, out clientVersion) ?? false)
            {
                this.ClientVersion = Option.Maybe(clientVersion);
            }

            string modelId = null;
            if (this.Link.Settings?.Properties?.TryGetValue(IotHubAmqpProperty.ModelId, out modelId) ?? false)
            {
                this.ModelId = Option.Maybe(modelId);
            }
        }

        public IAmqpLink Link { get; }

        public Uri LinkUri { get; }

        public abstract LinkType Type { get; }

        public virtual string CorrelationId { get; } = Guid.NewGuid().ToString();

        protected IIdentity Identity { get; }

        protected string ClientId => this.Identity.Id;

        protected IMessageConverter<AmqpMessage> MessageConverter { get; }

        protected IDeviceListener DeviceListener { get; private set; }

        protected IDictionary<string, string> BoundVariables { get; }

        protected Option<string> ClientVersion { get; }

        public Option<string> ModelId { get; }

        public async Task OpenAsync(TimeSpan timeout)
        {
            if (!await this.Authenticate())
            {
                throw new InvalidOperationException($"Unable to open {this.Type} link as the connection could not be authenticated");
            }

            this.DeviceListener = await this.connectionHandler.GetDeviceListener();

            await this.OnOpenAsync(timeout);
            await this.connectionHandler.RegisterLinkHandler(this);
            Events.Opened(this);
        }

        public async Task CloseAsync(TimeSpan timeout)
        {
            await this.Link.CloseAsync(timeout);
            Events.Closed(this);
        }

        protected abstract Task OnOpenAsync(TimeSpan timeout);

        protected async Task<bool> Authenticate()
        {
            IAmqpAuthenticator amqpAuth;
            IAmqpConnection connection = this.Link.Session.Connection;

            // Check if Principal is IAmqpAuthenticator
            if (connection.Principal is IAmqpAuthenticator connAuth)
            {
                amqpAuth = connAuth;
            }
            else if (connection.FindExtension<ICbsNode>() is IAmqpAuthenticator cbsAuth)
            {
                amqpAuth = cbsAuth;
            }
            else
            {
                throw new InvalidOperationException($"Unable to find authentication mechanism for AMQP connection for identity {this.Identity.Id}");
            }

            bool authenticated = await amqpAuth.AuthenticateAsync(this.Identity.Id, this.ModelId);
            if (authenticated)
            {
                string productInfo = string.Empty;
                this.ClientVersion
                    .Filter(c => !string.IsNullOrWhiteSpace(c))
                    .ForEach(c => productInfo = c);
                await this.metadataStore.SetMetadata(this.Identity.Id, productInfo, this.ModelId);
            }

            return authenticated;
        }

        protected virtual void OnLinkClosed(object sender, EventArgs args)
        {
            Events.Closed(this);
            this.connectionHandler.RemoveLinkHandler(this);
        }

        static class Events
        {
            const int IdStart = AmqpEventIds.LinkHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<LinkHandler>();

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
