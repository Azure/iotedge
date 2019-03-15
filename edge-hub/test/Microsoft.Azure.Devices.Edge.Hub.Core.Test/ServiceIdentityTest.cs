// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class ServiceIdentityTest
    {
        [Theory]
        [MemberData(nameof(GetEqualityTestData))]
        public void EqualityTest(ServiceIdentity identity1, ServiceIdentity identity2, bool areEqual)
        {
            // Act
            bool result = identity1.Equals(identity2);

            // Assert
            Assert.Equal(areEqual, result);
        }

        public static IEnumerable<object[]> GetEqualityTestData()
        {
            // Device identities - Equal
            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                true
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string> { "123", "iotEdge" }, new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "1234", new List<string> { "123", "iotEdge" }, new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                true
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                true
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(new X509ThumbprintAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(new X509ThumbprintAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                true
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.None), ServiceIdentityStatus.Disabled),
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.None), ServiceIdentityStatus.Disabled),
                true
            };

            // Module identities - Equal
            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                true
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string> { "123", "iotEdge" }, new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "m1", "1234", new List<string> { "123", "iotEdge" }, new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                true
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                true
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(new X509ThumbprintAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(new X509ThumbprintAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                true
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.None), ServiceIdentityStatus.Disabled),
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.None), ServiceIdentityStatus.Disabled),
                true
            };

            // Device identities - Not Equal
            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d2", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "1235", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string> { "123" }, new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string> { "123" }, new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "1234", new List<string> { "234" }, new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(new X509ThumbprintAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Disabled),
                false
            };

            // Module identities - Not Equal
            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d2", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "m2", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "m1", "1235", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string> { "123" }, new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string> { "123" }, new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "m1", "1234", new List<string> { "234" }, new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(new X509ThumbprintAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(new SymmetricKeyAuthentication("k1", "k2")), ServiceIdentityStatus.Enabled),
                false
            };

            yield return new object[]
            {
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Enabled),
                new ServiceIdentity("d1", "m1", "1234", new List<string>(), new ServiceAuthentication(ServiceAuthenticationType.CertificateAuthority), ServiceIdentityStatus.Disabled),
                false
            };
        }
    }
}
