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

    public abstract class LinkHandler : ILinkHandler
    {
        Option<IDeviceListener> deviceListener = Option.None<IDeviceListener>();
        readonly IConnectionProvider connectionProvider;
        AmqpAuthentication amqpAuthentication;

        protected LinkHandler(IAmqpLink link, Uri requestUri, IDictionary<string, string> boundVariables,
            IMessageConverter<AmqpMessage> messageConverter, IConnectionProvider connectionProvider)
        {
            // TODO: IoT Hub periodically validates that the authorization is still valid in this
            // class using a timer (except when the concrete sub-class is CbsLinkHandler or EventHubReceiveRedirectLinkHandler.
            // We need to evaluate whether it makes sense to do that in Edge Hub too. See the implementation in
            // AmqpGatewayProtocolHead.LinkHandler.IotHubStatusTimerCallback in service code.

            this.MessageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.BoundVariables = Preconditions.CheckNotNull(boundVariables, nameof(boundVariables));
            this.connectionProvider = Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));
            this.Link = Preconditions.CheckNotNull(link, nameof(link));
            this.LinkUri = Preconditions.CheckNotNull(requestUri, nameof(requestUri));
            this.Link.SafeAddClosed(this.OnLinkClosed);
        }

        protected IMessageConverter<AmqpMessage> MessageConverter { get; }

        protected Option<IDeviceListener> DeviceListener => this.deviceListener;

        protected IDictionary<string, string> BoundVariables { get; }

        public IAmqpLink Link { get; }

        public Uri LinkUri { get; }

        protected abstract string Name { get; }

        public async Task OpenAsync(TimeSpan timeout)
        {
            if (!this.Link.IsCbsLink())
            {
                if (!await this.Authenticate())
                {
                    throw new InvalidOperationException($"Unable to open {this.Name} link as connection is not authenticated");
                }
                await this.InitDeviceListener();
            }
            await this.OnOpenAsync(timeout);
        }

        protected abstract Task OnOpenAsync(TimeSpan timeout);

        protected async Task<bool> Authenticate() => (await this.GetAmqpAuthentication()).IsAuthenticated;

        async Task<AmqpAuthentication> GetAmqpAuthentication()
        {
            if (this.amqpAuthentication == null)
            {
                // Check if Principal is SaslPrincipal
                if (this.Link.Session.Connection.Principal is SaslPrincipal saslPrincipal)
                {
                    this.amqpAuthentication = saslPrincipal.AmqpAuthentication;
                }
                else
                {
                    // Else the connection uses CBS authentication. Get AmqpAuthentication from the CbsNode                    
                    var cbsNode = this.Link.Session.Connection.FindExtension<ICbsNode>();
                    if (cbsNode == null)
                    {
                        throw new InvalidOperationException("CbsNode is null");
                    }

                    this.amqpAuthentication = await cbsNode.GetAmqpAuthentication();
                }
            }
            return this.amqpAuthentication;
        }

        protected virtual void OnLinkClosed(object sender, EventArgs args) { }

        async Task InitDeviceListener()
        {
            AmqpAuthentication amqpAuth = await this.GetAmqpAuthentication();
            if (amqpAuth.IsAuthenticated)
            {
                IIdentity identity = amqpAuth.Identity.Expect(() => new InvalidOperationException("Authenticated CbsNode should have a valid Identity"));
                this.deviceListener = Option.Some(await this.connectionProvider.GetDeviceListenerAsync(identity));
            }
        }
    }
}
