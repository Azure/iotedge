// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class ServiceAuthentication
    {
        public ServiceAuthentication(SymmetricKeyAuthentication symmetricKeyAuthentication)
            : this(ServiceAuthenticationType.SymmetricKey, Preconditions.CheckNotNull(symmetricKeyAuthentication, nameof(symmetricKeyAuthentication)), null)
        {
        }

        public ServiceAuthentication(X509ThumbprintAuthentication x509ThumbprintAuthentication)
            : this(ServiceAuthenticationType.CertificateThumbprint, null, Preconditions.CheckNotNull(x509ThumbprintAuthentication, nameof(x509ThumbprintAuthentication)))
        {
        }

        public ServiceAuthentication(ServiceAuthenticationType type)
            : this(type, null, null)
        {
        }

        [JsonConstructor]
        ServiceAuthentication(ServiceAuthenticationType type, SymmetricKeyAuthentication symmetricKey, X509ThumbprintAuthentication x509Thumbprint)
        {
            Preconditions.CheckArgument(type != ServiceAuthenticationType.SymmetricKey || symmetricKey != null, $"{nameof(SymmetricKeyAuthentication)} should not be null when type is {ServiceAuthenticationType.SymmetricKey}");
            Preconditions.CheckArgument(type != ServiceAuthenticationType.CertificateThumbprint || x509Thumbprint != null, $"{nameof(X509ThumbprintAuthentication)} should not be null when type is {ServiceAuthenticationType.CertificateThumbprint}");

            this.Type = type;
            this.SymmetricKey = Option.Maybe(symmetricKey);
            this.X509Thumbprint = Option.Maybe(x509Thumbprint);
        }

        [JsonProperty("type")]
        public ServiceAuthenticationType Type { get; }

        [JsonProperty("symmetricKey")]
        [JsonConverter(typeof(OptionConverter<SymmetricKeyAuthentication>))]
        public Option<SymmetricKeyAuthentication> SymmetricKey { get; }

        [JsonProperty("x509Thumbprint")]
        [JsonConverter(typeof(OptionConverter<X509ThumbprintAuthentication>))]
        public Option<X509ThumbprintAuthentication> X509Thumbprint { get; }
    }
}
