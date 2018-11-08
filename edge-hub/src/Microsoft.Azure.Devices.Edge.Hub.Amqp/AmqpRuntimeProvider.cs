// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Encoding;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Amqp.X509;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class AmqpRuntimeProvider : IRuntimeProvider
    {
        static readonly AmqpSymbol LinkHandlerPropertyKey = new AmqpSymbol("AmqpProtocolHead.LinkHandler");
        readonly bool requireSecureTransport;
        readonly ILinkHandlerProvider linkHandlerProvider;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly IAuthenticator authenticator;
        readonly IConnectionProvider connectionProvider;
        readonly string iotHubHostName;
        readonly ICredentialsCache credentialsCache;

        public AmqpRuntimeProvider(
            ILinkHandlerProvider linkHandlerProvider,
            bool requireSecureTransport,
            IClientCredentialsFactory clientCredentialsFactory,
            IAuthenticator authenticator,
            string iotHubHostName,
            IConnectionProvider connectionProvider,
            ICredentialsCache credentialsCache)
        {
            this.linkHandlerProvider = Preconditions.CheckNotNull(linkHandlerProvider, nameof(linkHandlerProvider));
            this.requireSecureTransport = Preconditions.CheckNotNull(requireSecureTransport, nameof(requireSecureTransport));
            this.clientCredentialsFactory = Preconditions.CheckNotNull(clientCredentialsFactory, nameof(clientCredentialsFactory));
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.connectionProvider = Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));
            this.credentialsCache = Preconditions.CheckNotNull(credentialsCache, nameof(credentialsCache));
        }

        IAsyncResult ILinkFactory.BeginOpenLink(AmqpLink link, TimeSpan timeout, AsyncCallback callback, object state) =>
            this.OpenLinkAsync(link, timeout).ToAsyncResult(callback, state);

        AmqpConnection IConnectionFactory.CreateConnection(
            TransportBase transport,
            ProtocolHeader protocolHeader,
            bool isInitiator,
            AmqpSettings settings,
            AmqpConnectionSettings connectionSettings)
        {
            if (this.requireSecureTransport && !transport.IsSecure)
            {
                throw new AmqpException(AmqpErrorCode.NotAllowed, "AMQP transport is not secure");
            }

            var connection = new AmqpConnection(transport, protocolHeader, false, settings, connectionSettings)
            {
                SessionFactory = this
            };
            connection.Opening += this.OnConnectionOpening;

            return connection;
        }

        AmqpLink ILinkFactory.CreateLink(AmqpSession session, AmqpLinkSettings settings)
        {
            try
            {
                this.ValidateLinkSettings(settings);

                // Override AmqpLinkSetting MaxMessageSize to restrict it to Constants.AmqpMaxMessageSize
                if (settings.MaxMessageSize == null || settings.MaxMessageSize == 0 || settings.MaxMessageSize > Constants.AmqpMaxMessageSize)
                {
                    settings.MaxMessageSize = Constants.AmqpMaxMessageSize;
                }

                AmqpLink amqpLink;
                IAmqpLink wrappingAmqpLink;
                string linkAddress;
                if (settings.IsReceiver())
                {
                    amqpLink = new ReceivingAmqpLink(session, settings);
                    wrappingAmqpLink = new EdgeReceivingAmqpLink((ReceivingAmqpLink)amqpLink);
                    linkAddress = ((Target)settings.Target).Address.ToString();
                }
                else
                {
                    amqpLink = new SendingAmqpLink(session, settings);
                    wrappingAmqpLink = new EdgeSendingAmqpLink((SendingAmqpLink)amqpLink);
                    linkAddress = ((Source)settings.Source).Address.ToString();
                }

                // TODO: implement the rules below
                // Link address may be of the forms:
                //
                //  amqp[s]://my.servicebus.windows.net/a/b     <-- FQ address where host name should match connection remote host name
                //  amqp[s]:a/b                                 <-- path relative to hostname specified in OPEN
                //  a/b                                         <-- pre-global addressing style path relative to hostname specified in OPEN
                //  /a/b                                        <-- same as above
                Uri linkUri;
                if (!linkAddress.StartsWith(Constants.AmqpsScheme, StringComparison.OrdinalIgnoreCase))
                {
                    string host = session.Connection.Settings.RemoteHostName;
                    linkUri = new Uri("amqps://" + host + linkAddress.EnsureStartsWith('/'));
                }
                else
                {
                    linkUri = new Uri(linkAddress, UriKind.RelativeOrAbsolute);
                }

                ILinkHandler linkHandler = this.linkHandlerProvider.Create(wrappingAmqpLink, linkUri);
                amqpLink.Settings.AddProperty(LinkHandlerPropertyKey, linkHandler);
                return amqpLink;
            }
            catch (Exception e) when (!ExceptionEx.IsFatal(e))
            {
                // Don't throw here because we cannot provide error info. Instead delay and throw from Link.Open.
                return new FaultedLink(e, session, settings);
            }
        }

        AmqpSession ISessionFactory.CreateSession(AmqpConnection connection, AmqpSessionSettings settings) => new AmqpSession(connection, settings, this);

        void ILinkFactory.EndOpenLink(IAsyncResult result) => TaskEx.EndAsyncResult(result);

        void OnConnectionOpening(object sender, OpenEventArgs e)
        {
            var command = (Open)e.Command;

            // 'command.IdleTimeOut' is the Idle time out specified in the client OPEN frame
            // Server will send heart beats honoring this timeout(every 7/8 of IdleTimeout)
            if (command.IdleTimeOut == null || command.IdleTimeOut == 0)
            {
                command.IdleTimeOut = Constants.DefaultAmqpHeartbeatSendInterval;
            }
            else if (command.IdleTimeOut < Constants.MinimumAmqpHeartbeatSendInterval)
            {
                throw new EdgeHubConnectionException($"Connection idle timeout specified is less than minimum acceptable value: {Constants.MinimumAmqpHeartbeatSendInterval}");
            }

            var amqpConnection = (AmqpConnection)sender;

            // If the AmqpConnection does not use username/password or certs, create a CbsNode for the connection
            // and add it to the Extensions
            if (!(amqpConnection.Principal is SaslPrincipal || amqpConnection.Principal is X509Principal))
            {
                ICbsNode cbsNode = new CbsNode(this.clientCredentialsFactory, this.iotHubHostName, this.authenticator, this.credentialsCache);
                amqpConnection.Extensions.Add(cbsNode);
            }

            IConnectionHandler connectionHandler = new ConnectionHandler(new EdgeAmqpConnection(amqpConnection), this.connectionProvider);
            amqpConnection.Extensions.Add(connectionHandler);
        }

        Task OpenLinkAsync(AmqpLink link, TimeSpan timeout)
        {
            try
            {
                if (link is FaultedLink faultedLink)
                {
                    throw faultedLink.Exception;
                }

                if (link.Settings.Properties == null || !link.Settings.Properties.TryRemoveValue(LinkHandlerPropertyKey, out ILinkHandler linkHandler))
                {
                    throw new InvalidOperationException("LinkHandler cannot be null");
                }

                return linkHandler.OpenAsync(timeout);
            }
            catch (Exception exception) when (!ExceptionEx.IsFatal(exception))
            {
                // TODO: Handle token expiry exception case here by closing AMQP connection.
                throw;
            }
        }

        void ValidateLinkSettings(AmqpLinkSettings settings)
        {
            if (settings.IsReceiver())
            {
                if (!(settings.Target is Target target) || target.Address.ToString().Length == 0)
                {
                    throw new InvalidOperationException("Link target address is null or empty");
                }
            }
            else
            {
                if (!(settings.Source is Source source) || source.Address.ToString().Length == 0)
                {
                    throw new InvalidOperationException("Link source address is null or empty");
                }
            }
        }
    }
}
