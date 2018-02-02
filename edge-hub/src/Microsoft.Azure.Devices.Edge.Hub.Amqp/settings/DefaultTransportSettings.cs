// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Util;

    public class DefaultTransportSettings : ITransportSettings
    {
        public DefaultTransportSettings(
            string scheme,
            string hostName,
            int port,
            X509Certificate2 tlsCertificate
        )
        {
            this.HostName = Preconditions.CheckNonWhiteSpace(hostName, nameof(hostName));

            var address = new UriBuilder
            {
                Host = hostName,
                Port = Preconditions.CheckRange(port, 0, ushort.MaxValue),
                Scheme = Preconditions.CheckNonWhiteSpace(scheme, nameof(scheme))
            };

            var tcpSettings = new TcpTransportSettings()
            {
                Host = address.Host,
                Port = address.Port
            };

            // NOTE:
            //  We don't support X509 client certs as an authentication mechanism
            //  yet. When we do, we'll want to incorporate that here.
            this.Settings = new TlsTransportSettings(tcpSettings, false)
            {
                TargetHost = address.Host,
                Certificate = Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate)),
                // NOTE: The following property doesn't appear to be used by the AMQP library.
                //       Not sure that setting this to true/false makes any difference!
                CheckCertificateRevocation = false
            };

            // NOTE: We don't support X509 client cert auth yet. When we do the following
            //       line becomes relevant.
            // tlsSettings.CertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        }

        public string HostName { get; }

        public TransportSettings Settings { get; }
    }
}
