// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Certificate
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util.Edged.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using CertificateHelper = Microsoft.Azure.Devices.Edge.Util.CertificateHelper;
    using TestCertificateHelper = Microsoft.Azure.Devices.Edge.Util.Test.Common.CertificateHelper;

    [Unit]
    public class CertificateHelperTest
    {
        [Fact]
        public void GetThumbprintNullCertThrows()
        {
            Assert.Throws<ArgumentNullException>(() => CertificateHelper.GetSha256Thumbprint(null));
        }

        [Fact]
        public void BuildCertificateListSuccess()
        {
            X509Certificate2 cert = TestCertificateHelper.GenerateSelfSignedCert("top secret");
            (IList<X509Certificate2> certs, Option<string> errors) = CertificateHelper.BuildCertificateList(cert);
            Assert.True(certs.Count == 1);
            Assert.False(errors.HasValue);
        }

        [Fact]
        public void ValidateCertNullArgumentsThrows()
        {
            Assert.Throws<ArgumentNullException>(() => CertificateHelper.ValidateCert(null, new X509Certificate2[] { }, new X509Certificate2[] { }));
            Assert.Throws<ArgumentNullException>(() => CertificateHelper.ValidateCert(new X509Certificate2(), null, new X509Certificate2[] { }));
            Assert.Throws<ArgumentNullException>(() => CertificateHelper.ValidateCert(new X509Certificate2(), new X509Certificate2[] { }, null));
        }

        [Fact]
        public void ValidateCertSuccess()
        {
            X509Certificate2 cert = TestCertificateHelper.GenerateSelfSignedCert("top secret");
            (bool validated, Option<string> errors) = CertificateHelper.ValidateCert(cert, new[] { cert }, new[] { cert });
            Assert.True(validated);
            Assert.False(errors.HasValue);
        }

        [Fact]
        public void ValidateCertNoMatchFailure()
        {
            X509Certificate2 cert = TestCertificateHelper.GenerateSelfSignedCert("top secret");
            X509Certificate2 root = TestCertificateHelper.GenerateSelfSignedCert("root");
            (bool validated, Option<string> errors) = CertificateHelper.ValidateCert(cert, new[] { cert }, new[] { root });
            Assert.False(validated);
            Assert.True(errors.HasValue);
        }

        [Fact]
        public void ClientCertCallbackNullArgumentThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
            CertificateHelper.ValidateClientCert(null, new X509Chain(),
                Option.Some<IList<X509Certificate2>>(new X509Certificate2[] { }), Logger.Factory.CreateLogger("something")));
            Assert.Throws<ArgumentNullException>(() =>
            CertificateHelper.ValidateClientCert(new X509Certificate2(), null,
                Option.Some<IList<X509Certificate2>>(new X509Certificate2[] { }), Logger.Factory.CreateLogger("something")));
            Assert.Throws<ArgumentNullException>(() =>
            CertificateHelper.ValidateClientCert(new X509Certificate2(), new X509Chain(),
                Option.Some<IList<X509Certificate2>>(new X509Certificate2[] { }), null));
        }

        [Fact]
        public void ClientCertCallbackNoCaCertsFails()
        {
            X509Certificate2 cert = TestCertificateHelper.GenerateSelfSignedCert("top secret");
            Assert.False(CertificateHelper.ValidateClientCert(cert, new X509Chain(),
                Option.None<IList<X509Certificate2>>(), Logger.Factory.CreateLogger("something")));
        }

        [Fact]
        public void GetCertsAtPathNullArgumentFails()
        {
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetCertsAtPath(null));
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetCertsAtPath(""));
        }

        [Fact]
        public void ExtractCertsNullArgumentFails()
        {
            Assert.Throws<ArgumentException>(() => CertificateHelper.ExtractCertsFromPem(null));
            Assert.Throws<ArgumentException>(() => CertificateHelper.ExtractCertsFromPem(""));
        }

        [Fact]
        public void GetServerCertificateAndChainFromFileRaisesArgExceptionWithInvalidCertFile()
        {
            string testFile = Path.GetRandomFileName();
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile(null, testFile));
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile("", testFile));
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile("   ", testFile));
        }

        [Fact]
        public void GetServerCertificateAndChainFromFileRaisesArgExceptionWithInvalidPrivateKeyFile()
        {
            string testFile = Path.GetRandomFileName();
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile(testFile, null));
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile(testFile, ""));
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile(testFile, "   "));
        }

        [Fact]
        public void ParseTrustedBundleFromFileRaisesExceptionWithInvalidTBFile()
        {
            string testFile = Path.GetRandomFileName();
            Assert.Throws<ArgumentException>(() => CertificateHelper.ParseTrustedBundleFromFile(null));
            Assert.Throws<ArgumentException>(() => CertificateHelper.ParseTrustedBundleFromFile(""));
            Assert.Throws<ArgumentException>(() => CertificateHelper.ParseTrustedBundleFromFile("   "));
            Assert.Throws<ArgumentException>(() => CertificateHelper.ParseTrustedBundleFromFile(testFile));
        }

        [Fact]
        public void ParseTrustBundleNullResponseRaisesException()
        {
            TrustBundleResponse response = null;
            Assert.Throws<ArgumentException>(() => CertificateHelper.ParseTrustBundleResponse(response));
        }

        [Fact]
        public void ParseTrustBundleEmptyResponseReturnsEmptyList()
        {
            var response = new TrustBundleResponse()
            {
                Certificate = "  ",
            };
            IEnumerable<X509Certificate2> certs = CertificateHelper.ParseTrustBundleResponse(response);
            Assert.Equal(certs.Count(), 0);
        }

        [Fact]
        public void ParseTrustBundleInvalidResponseReturnsEmptyList()
        {
            var response = new TrustBundleResponse()
            {
                Certificate = "somewhere over the rainbow",
            };
            IEnumerable<X509Certificate2> certs = CertificateHelper.ParseTrustBundleResponse(response);
            Assert.Equal(certs.Count(), 0);
        }

        [Fact]
        public void ParseTrustBundleResponseWithOneCertReturnsNonEmptyList()
        {
            var response = new TrustBundleResponse()
            {
                Certificate = $"{TestCertificateHelper.CertificatePem}\n",
            };
            IEnumerable<X509Certificate2> certs = CertificateHelper.ParseTrustBundleResponse(response);
            Assert.Equal(certs.Count(), 1);
        }

        [Fact]
        public void ParseTrustBundleResponseWithMultipleCertReturnsNonEmptyList()
        {
            var response = new TrustBundleResponse()
            {
                Certificate = $"{TestCertificateHelper.CertificatePem}\n{TestCertificateHelper.CertificatePem}",
            };
            IEnumerable<X509Certificate2> certs = CertificateHelper.ParseTrustBundleResponse(response);
            Assert.Equal(certs.Count(), 2);
        }

        [Fact]
        public void ParseCertificatesSingleShouldReturnCetificate()
        {
            IList<string> pemCerts = CertificateHelper.ParsePemCerts(TestCertificateHelper.CertificatePem);
            IEnumerable<X509Certificate2> certs = CertificateHelper.GetCertificatesFromPem(pemCerts);

            Assert.Equal(certs.Count(), 1);
        }

        [Fact]
        public void ParseCertificatesMultipleCertsShouldReturnCetificates()
        {
            IList<string> pemCerts = CertificateHelper.ParsePemCerts(TestCertificateHelper.CertificatePem + TestCertificateHelper.CertificatePem);
            IEnumerable<X509Certificate2> certs = CertificateHelper.GetCertificatesFromPem(pemCerts);

            Assert.Equal(certs.Count(), 2);
        }

        [Fact]
        public void ParseCertificatesWithNonCertificatesEntriesShouldReturnCetificates()
        {
            IList<string> pemCerts = CertificateHelper.ParsePemCerts(TestCertificateHelper.CertificatePem + TestCertificateHelper.CertificatePem + "test");
            IEnumerable<X509Certificate2> certs = CertificateHelper.GetCertificatesFromPem(pemCerts);

            Assert.Equal(certs.Count(), 2);
        }

        [Fact]
        public void ParseCertificatesNoCertificatesEntriesShouldReturnNoCetificates()
        {
            IList<string> pemCerts = CertificateHelper.ParsePemCerts("test");
            IEnumerable<X509Certificate2> certs = CertificateHelper.GetCertificatesFromPem(pemCerts);

            Assert.Equal(certs.Count(), 0);
        }

        [Fact]
        public void ParseCertificatesResponseInvalidCertificateShouldThrow()
        {
            var response = new CertificateResponse()
            {
                Certificate = "InvalidCert",
            };
            Assert.Throws<InvalidOperationException>(() => CertificateHelper.ParseCertificateResponse(response));
        }

        [Fact]
        public void ParseCertificatesResponseInvalidKeyShouldThrow()
        {
            var response = new CertificateResponse()
            {
                Certificate = TestCertificateHelper.CertificatePem,
                Expiration = DateTime.UtcNow.AddDays(1),
                PrivateKey = new PrivateKey()
                {
                    Bytes = "InvalidKey"
                }
            };

            Assert.Throws<InvalidOperationException>(() => CertificateHelper.ParseCertificateResponse(response));
        }

        [Fact]
        public void ParseCertificatesResponseShouldReturnCert()
        {
            TestCertificateHelper.GenerateSelfSignedCert("top secret").Export(X509ContentType.Cert);
            var response = new CertificateResponse()
            {
                Certificate = $"{TestCertificateHelper.CertificatePem}\n{TestCertificateHelper.CertificatePem}",
                Expiration = DateTime.UtcNow.AddDays(1),
                PrivateKey = new PrivateKey()
                {
                    Bytes = TestCertificateHelper.PrivateKeyPem
                }
            };
            (X509Certificate2 cert, IEnumerable<X509Certificate2> chain) = CertificateHelper.ParseCertificateResponse(response);

            var expected = new X509Certificate2(Encoding.UTF8.GetBytes(TestCertificateHelper.CertificatePem));
            Assert.Equal(expected, cert);
            Assert.True(cert.HasPrivateKey);
            Assert.Equal(chain.Count(), 1);
            Assert.Equal(expected, chain.First());
        }

        [Fact]
        public void ParseCertificateAndKeyShouldReturnCertAndKey()
        {
            TestCertificateHelper.GenerateSelfSignedCert("top secret").Export(X509ContentType.Cert);
            (X509Certificate2 cert, IEnumerable<X509Certificate2> chain) = CertificateHelper.ParseCertificateAndKey(TestCertificateHelper.CertificatePem, TestCertificateHelper.PrivateKeyPem);

            var expected = new X509Certificate2(Encoding.UTF8.GetBytes(TestCertificateHelper.CertificatePem));
            Assert.Equal(expected, cert);
            Assert.True(cert.HasPrivateKey);
            Assert.Equal(chain.Count(), 0);
        }

        public void ParseMultipleCertificateAndKeyShouldReturnCertAndKey()
        {
            TestCertificateHelper.GenerateSelfSignedCert("top secret").Export(X509ContentType.Cert);
            string certificate = $"{TestCertificateHelper.CertificatePem}\n{TestCertificateHelper.CertificatePem}";
            (X509Certificate2 cert, IEnumerable<X509Certificate2> chain) = CertificateHelper.ParseCertificateAndKey(certificate, TestCertificateHelper.PrivateKeyPem);

            var expected = new X509Certificate2(Encoding.UTF8.GetBytes(TestCertificateHelper.CertificatePem));
            Assert.Equal(expected, cert);
            Assert.True(cert.HasPrivateKey);
            Assert.Equal(chain.Count(), 1);
            Assert.Equal(expected, chain.First());
        }
    }
}
