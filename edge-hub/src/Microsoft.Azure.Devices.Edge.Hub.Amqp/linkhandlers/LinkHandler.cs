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
        IDeviceListener deviceListener;

        protected LinkHandler(IAmqpLink link, Uri requestUri,
            IDictionary<string, string> boundVariables, IMessageConverter<AmqpMessage> messageConverter)
        {
            // TODO: IoT Hub periodically validates that the authorization is still valid in this
            // class using a timer (except when the concrete sub-class is CbsLinkHandler or EventHubReceiveRedirectLinkHandler.
            // We need to evaluate whether it makes sense to do that in Edge Hub too. See the implementation in
            // AmqpGatewayProtocolHead.LinkHandler.IotHubStatusTimerCallback in service code.

            this.MessageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.BoundVariables = Preconditions.CheckNotNull(boundVariables, nameof(boundVariables));
            this.Link = Preconditions.CheckNotNull(link, nameof(link));
            this.LinkUri = Preconditions.CheckNotNull(requestUri, nameof(requestUri));
            this.Link.SafeAddClosed(this.OnLinkClosed);
            this.ConnectionHandler = this.Link.Session.Connection.FindExtension<IConnectionHandler>();
        }

        protected IMessageConverter<AmqpMessage> MessageConverter { get; }

        protected IDeviceListener DeviceListener => this.deviceListener;

        protected IDictionary<string, string> BoundVariables { get; }

        protected abstract string Name { get; }

        protected IIdentity Identity => this.deviceListener?.Identity;

        protected IConnectionHandler ConnectionHandler { get; }

        public IAmqpLink Link { get; }

        public Uri LinkUri { get; }

        public async Task OpenAsync(TimeSpan timeout)
        {
            if (!this.Link.IsCbsLink())
            {
                if (!await this.Authenticate())
                {
                    throw new InvalidOperationException($"Unable to open {this.Name} link as connection is not authenticated");
                }

                this.deviceListener = await this.ConnectionHandler.GetDeviceListener();
            }
            await this.OnOpenAsync(timeout);
        }

        protected abstract Task OnOpenAsync(TimeSpan timeout);

        protected async Task<bool> Authenticate() => (await this.ConnectionHandler.GetAmqpAuthentication()).IsAuthenticated;

        protected virtual void OnLinkClosed(object sender, EventArgs args) { }
    }
}
