// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Extensions
{
    using Microsoft.AspNetCore.Http.Features;

    public static class CertContext // TODO: Replace hacky POC
    {
        public static TlsConnectionFeatureExtended TlsConnectionFeatureExtended { get;  set; }
        public static TlsConnectionFeature TlsConnectionFeature { get;  set; }
    }
}
