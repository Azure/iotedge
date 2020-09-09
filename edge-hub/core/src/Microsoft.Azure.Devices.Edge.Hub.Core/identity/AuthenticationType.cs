// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    public enum AuthenticationType
    {
        None,
        SasKey,
        Token,
        X509Cert,
        IoTEdged
    }
}
