// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeTlsTransportListener : TlsTransportListener
    {
        readonly IClientCredentialsFactory clientCredentialsProvider;
        readonly IAuthenticator authenticator;

        public EdgeTlsTransportListener(
            TlsTransportSettings transportSettings,
            IAuthenticator authenticator,
            IClientCredentialsFactory clientCredentialsProvider)
            : base(transportSettings)
        {
            this.clientCredentialsProvider = Preconditions.CheckNotNull(clientCredentialsProvider, nameof(clientCredentialsProvider));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
        }

        protected override TlsTransport OnCreateTransport(TransportBase innerTransport, TlsTransportSettings tlsTransportSettings) =>
            new EdgeTlsTransport(innerTransport, tlsTransportSettings, this.authenticator, this.clientCredentialsProvider);
    }
}
