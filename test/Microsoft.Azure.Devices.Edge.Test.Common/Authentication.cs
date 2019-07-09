// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.ComponentModel;

    public enum Authentication
    {
        Sas,
        X509Certificate,
        X509Thumbprint
    }

    public static class AuthenticationExtensions
    {
        public static AuthenticationType ToAuthenticationType(this Authentication auth)
        {
            switch (auth)
            {
                case Authentication.Sas:
                    return AuthenticationType.Sas;
                case Authentication.X509Certificate:
                    return AuthenticationType.CertificateAuthority;
                case Authentication.X509Thumbprint:
                    throw new NotImplementedException();
                default:
                    throw new InvalidEnumArgumentException();
            }
        }
    }

}
