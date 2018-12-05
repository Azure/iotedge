// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeHubTlsTransportSettings : TlsTransportSettings
    {
        readonly string iotHubHostName;
        readonly IClientCredentialsFactory clientCredentialsProvider;
        readonly IAuthenticator authenticator;

        public EdgeHubTlsTransportSettings(
            TransportSettings innerSettings,
            bool isInitiator,
            string iotHubHostName,
            IAuthenticator authenticator,
            IClientCredentialsFactory clientCredentialsProvider)
            : base(innerSettings, isInitiator)
        {
            this.iotHubHostName = Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.clientCredentialsProvider = Preconditions.CheckNotNull(clientCredentialsProvider, nameof(clientCredentialsProvider));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
        }

        public override TransportListener CreateListener()
        {
            if (this.Certificate == null)
            {
                throw new InvalidOperationException("Server certificate must be set");
            }

            return new EdgeHubTlsTransportListener(this, this.iotHubHostName, this.authenticator, this.clientCredentialsProvider);
        }
    }
}
