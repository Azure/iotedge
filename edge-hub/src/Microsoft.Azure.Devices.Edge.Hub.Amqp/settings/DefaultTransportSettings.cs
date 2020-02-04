// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings
{
    using System;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DefaultTransportSettings : ITransportSettings
    {
        public DefaultTransportSettings(
            string scheme,
            string hostName,
            int port,
            X509Certificate2 tlsCertificate,
            bool clientCertAuthAllowed,
            IAuthenticator authenticator,
            IClientCredentialsFactory clientCredentialsProvider,
            SslProtocols sslProtocols)
        {
            this.HostName = Preconditions.CheckNonWhiteSpace(hostName, nameof(hostName));
            Preconditions.CheckNotNull(clientCredentialsProvider, nameof(clientCredentialsProvider));
            Preconditions.CheckNotNull(authenticator, nameof(authenticator));

            var address = new UriBuilder
            {
                Host = hostName,
                Port = Preconditions.CheckRange(port, 0, ushort.MaxValue, nameof(port)),
                Scheme = Preconditions.CheckNonWhiteSpace(scheme, nameof(scheme))
            };

            var tcpSettings = new TcpTransportSettings()
            {
                Host = address.Host,
                Port = address.Port
            };

            var tlsSettings = new EdgeTlsTransportSettings(tcpSettings, false, authenticator, clientCredentialsProvider)
            {
                TargetHost = address.Host,
                Certificate = Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate)),
                // NOTE: The following property doesn't appear to be used by the AMQP library.
                //       Not sure that setting this to true/false makes any difference!
                CheckCertificateRevocation = false,
                Protocols = sslProtocols
            };

            if (clientCertAuthAllowed == true)
            {
                tlsSettings.CertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            }

            this.Settings = tlsSettings;
        }

        public string HostName { get; }

        public TransportSettings Settings { get; }
    }
}
