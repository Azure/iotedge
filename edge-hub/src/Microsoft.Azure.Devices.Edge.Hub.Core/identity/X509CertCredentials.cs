// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Util;

    public class X509CertCredentials : ICertificateCredentials
    {
        public X509CertCredentials(IIdentity identity, string productInfo, X509Certificate2 clientCertificate, IList<X509Certificate2> clientCertificateChain)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.AuthenticationType = AuthenticationType.X509Cert;
            this.ProductInfo = productInfo ?? string.Empty;
            this.ClientCertificate = Preconditions.CheckNotNull(clientCertificate, nameof(clientCertificate));
            this.ClientCertificateChain = Preconditions.CheckNotNull(clientCertificateChain, nameof(clientCertificateChain)).ToList();
        }

        public IIdentity Identity { get; }

        public AuthenticationType AuthenticationType { get; }

        public string ProductInfo { get; }

        public X509Certificate2 ClientCertificate { get; }

        public IList<X509Certificate2> ClientCertificateChain { get; }
    }
}
