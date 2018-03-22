// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class X509CertCredentials : IClientCredentials
    {
        public X509CertCredentials(IIdentity identity, string productInfo)
        {
            this.Identity = Preconditions.CheckNotNull(identity, nameof(identity));
            this.AuthenticationType = AuthenticationType.X509Cert;
            this.ProductInfo = productInfo ?? string.Empty;
        }

        public IIdentity Identity { get; }

        public AuthenticationType AuthenticationType { get; }

        public string ProductInfo { get; }
    }
}
