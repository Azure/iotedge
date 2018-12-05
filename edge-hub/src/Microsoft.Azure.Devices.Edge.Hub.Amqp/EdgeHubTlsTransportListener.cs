// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeHubTlsTransportListener : TlsTransportListener
    {
        readonly string iotHubHostName;
        readonly IClientCredentialsFactory clientCredentialsProvider;
        readonly IAuthenticator authenticator;

        public EdgeHubTlsTransportListener(
            TlsTransportSettings transportSettings,
            string iotHubHostName,
            IAuthenticator authenticator,
            IClientCredentialsFactory clientCredentialsProvider)
            : base(transportSettings)
        {
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.clientCredentialsProvider = Preconditions.CheckNotNull(clientCredentialsProvider, nameof(clientCredentialsProvider));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
        }

        protected override TlsTransport OnCreateTransport(TransportBase innerTransport, TlsTransportSettings tlsTransportSettings)
        {
            return new EdgeHubTlsTransport(innerTransport, tlsTransportSettings, this.iotHubHostName, this.authenticator, this.clientCredentialsProvider);
        }
    }
}
