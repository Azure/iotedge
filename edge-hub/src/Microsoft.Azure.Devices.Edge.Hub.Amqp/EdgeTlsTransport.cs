// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Amqp.X509;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeTlsTransport : TlsTransport
    {
        readonly IClientCredentialsFactory clientCredentialsProvider;
        readonly IAuthenticator authenticator;
        private IList<X509Certificate2> remoteCertificateChain;

        public EdgeTlsTransport(
            TransportBase innerTransport,
            TlsTransportSettings tlsSettings,
            IAuthenticator authenticator,
            IClientCredentialsFactory clientCredentialsProvider)
            : base(innerTransport, tlsSettings)
        {
            this.clientCredentialsProvider = Preconditions.CheckNotNull(clientCredentialsProvider, nameof(clientCredentialsProvider));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.remoteCertificateChain = null;
        }

        protected override X509Principal CreateX509Principal(X509Certificate2 certificate)
        {
            var principal = new EdgeX509Principal(new X509CertificateIdentity(certificate, true),
                                                     this.remoteCertificateChain,
                                                     this.authenticator,
                                                     this.clientCredentialsProvider);
            // release chain elements from here since principal has this
            this.remoteCertificateChain = null;
            return principal;
        }

        protected override bool ValidateRemoteCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // copy of the chain elements since they are destroyed after this method completes 
            this.remoteCertificateChain = chain == null ? new List<X509Certificate2>() :
                                                          chain.ChainElements.Cast<X509ChainElement>().Select(element => element.Certificate).ToList();
            return base.ValidateRemoteCertificate(sender, certificate, chain, sslPolicyErrors);
        }
    }
}
