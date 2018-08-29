// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ServiceAuthenticationType
    {
        SymmetricKey,
        CertificateThumbprint,
        CertificateAuthority,
        None,
    }
}
