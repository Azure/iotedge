// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeTlsTransportSettings : TlsTransportSettings
    {
        readonly IClientCredentialsFactory clientCredentialsProvider;
        readonly IAuthenticator authenticator;

        public EdgeTlsTransportSettings(
            TransportSettings innerSettings,
            bool isInitiator,
            IAuthenticator authenticator,
            IClientCredentialsFactory clientCredentialsProvider)
            : base(innerSettings, isInitiator)
        {
            this.clientCredentialsProvider = Preconditions.CheckNotNull(clientCredentialsProvider, nameof(clientCredentialsProvider));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
        }

        public override TransportListener CreateListener()
        {
            if (this.Certificate == null)
            {
                throw new InvalidOperationException("Server certificate must be set");
            }

            return new EdgeTlsTransportListener(this, this.authenticator, this.clientCredentialsProvider);
        }
    }
}
