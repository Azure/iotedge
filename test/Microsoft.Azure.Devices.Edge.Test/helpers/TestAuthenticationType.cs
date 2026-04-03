// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System.ComponentModel;
    using Microsoft.Azure.Devices;

    public enum TestAuthenticationType
    {
        SasInScope,
        SasOutOfScope,
        CertificateAuthority,
        SelfSignedPrimary,
        SelfSignedSecondary
    }

    public static class TestAuthenticationTypeExtensions
    {
        public static ClientAuthenticationType ToAuthenticationType(this TestAuthenticationType authType)
        {
            switch (authType)
            {
                case TestAuthenticationType.SasInScope:
                case TestAuthenticationType.SasOutOfScope:
                    return ClientAuthenticationType.Sas;
                case TestAuthenticationType.CertificateAuthority:
                    return ClientAuthenticationType.CertificateAuthority;
                case TestAuthenticationType.SelfSignedPrimary:
                case TestAuthenticationType.SelfSignedSecondary:
                    return ClientAuthenticationType.SelfSigned;
                default:
                    throw new InvalidEnumArgumentException();
            }
        }

        public static bool UseSecondaryCertificate(this TestAuthenticationType authType)
        {
            switch (authType)
            {
                case TestAuthenticationType.SasInScope:
                case TestAuthenticationType.SasOutOfScope:
                case TestAuthenticationType.CertificateAuthority:
                case TestAuthenticationType.SelfSignedPrimary:
                    return false;
                case TestAuthenticationType.SelfSignedSecondary:
                    return true;
                default:
                    throw new InvalidEnumArgumentException();
            }
        }
    }
}
