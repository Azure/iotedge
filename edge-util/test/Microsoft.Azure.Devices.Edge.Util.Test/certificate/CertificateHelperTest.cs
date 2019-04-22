// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Certificate
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
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
            (IList<X509Certificate2> certs, Option<string> errors) = CertificateHelper.BuildCertificateList(cert, Option.None<IList<X509Certificate2>>());
            Assert.True(certs.Count == 1);
            Assert.False(errors.HasValue);
        }

        [Fact]
        public void ValidateCertNullArgumentsThrows()
        {
            var trustedCACerts = Option.None<IList<X509Certificate2>>();
            Assert.Throws<ArgumentNullException>(() => CertificateHelper.ValidateCert(null, new X509Certificate2[] { }, trustedCACerts));
            Assert.Throws<ArgumentNullException>(() => CertificateHelper.ValidateCert(new X509Certificate2(), null, trustedCACerts));
        }

        [Fact]
        public void ValidateCertSuccess()
        {
            var trustedCACerts = Option.None<IList<X509Certificate2>>();
            X509Certificate2 cert = TestCertificateHelper.GenerateSelfSignedCert("top secret");
            (bool validated, Option<string> errors) = CertificateHelper.ValidateCert(cert, new[] { cert }, trustedCACerts);
            Assert.True(validated);
            Assert.False(errors.HasValue);
        }

        [Fact]
        public void ValidateCertNoMatchFailure()
        {
            X509Certificate2 cert = TestCertificateHelper.GenerateSelfSignedCert("top secret");
            X509Certificate2 root = TestCertificateHelper.GenerateSelfSignedCert("root");
            IList<X509Certificate2> ca = new List<X509Certificate2>() { root };
            (bool validated, Option<string> errors) = CertificateHelper.ValidateCert(cert, new[] { cert }, Option.Some(ca));
            Assert.False(validated);
            Assert.True(errors.HasValue);
        }

        [Fact]
        public void ClientCertCallbackNullArgumentThrows()
        {
            var trustedCACerts = Option.None<IList<X509Certificate2>>();
            Assert.Throws<ArgumentNullException>(
                () =>
                    CertificateHelper.ValidateClientCert(null, new List<X509Certificate2>(), trustedCACerts, Logger.Factory.CreateLogger("something")));
            Assert.Throws<ArgumentNullException>(
                () =>
                    CertificateHelper.ValidateClientCert(new X509Certificate2(), null, trustedCACerts, Logger.Factory.CreateLogger("something")));
        }

        [Fact]
        public void ClientCertCallbackNoCaCertsFails()
        {
            X509Certificate2 cert = TestCertificateHelper.GenerateSelfSignedCert("top secret");
            IList<X509Certificate2> ca = new List<X509Certificate2>();
            var trustedCACerts = Option.Some(ca);
            Assert.False(CertificateHelper.ValidateClientCert(cert, new List<X509Certificate2>(), trustedCACerts, Logger.Factory.CreateLogger("something")));
        }

        [Fact]
        public void ExtractCertsNullArgumentFails()
        {
            Assert.Throws<ArgumentException>(() => CertificateHelper.ExtractCertsFromPem(null));
            Assert.Throws<ArgumentException>(() => CertificateHelper.ExtractCertsFromPem(string.Empty));
        }

        [Fact]
        public void GetServerCertificateAndChainFromFileRaisesArgExceptionWithInvalidCertFile()
        {
            string testFile = Path.GetRandomFileName();
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile(null, testFile));
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile(string.Empty, testFile));
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile("   ", testFile));
        }

        [Fact]
        public void GetServerCertificateAndChainFromFileRaisesArgExceptionWithInvalidPrivateKeyFile()
        {
            string testFile = Path.GetRandomFileName();
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile(testFile, null));
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile(testFile, string.Empty));
            Assert.Throws<ArgumentException>(() => CertificateHelper.GetServerCertificateAndChainFromFile(testFile, "   "));
        }

        [Fact]
        public void ParseTrustedBundleFromFileRaisesExceptionWithInvalidTBFile()
        {
            string testFile = Path.GetRandomFileName();
            Assert.Throws<ArgumentException>(() => CertificateHelper.ParseTrustedBundleFromFile(null));
            Assert.Throws<ArgumentException>(() => CertificateHelper.ParseTrustedBundleFromFile(string.Empty));
            Assert.Throws<ArgumentException>(() => CertificateHelper.ParseTrustedBundleFromFile("   "));
            Assert.Throws<ArgumentException>(() => CertificateHelper.ParseTrustedBundleFromFile(testFile));
        }

        [Fact]
        public void ParseTrustBundleNullResponseRaisesException()
        {
            string response = null;
            Assert.Throws<ArgumentNullException>(() => CertificateHelper.ParseTrustedBundleCerts(response));
        }

        [Fact]
        public void ParseTrustBundleEmptyResponseReturnsEmptyList()
        {
            string trustBundle = "  ";
            IEnumerable<X509Certificate2> certs = CertificateHelper.ParseTrustedBundleCerts(trustBundle);
            Assert.Empty(certs);
        }

        [Fact]
        public void ParseTrustBundleInvalidResponseReturnsEmptyList()
        {
            string trustBundle = "somewhere over the rainbow";
            IEnumerable<X509Certificate2> certs = CertificateHelper.ParseTrustedBundleCerts(trustBundle);
            Assert.Empty(certs);
        }

        [Fact]
        public void ParseTrustBundleResponseWithOneCertReturnsNonEmptyList()
        {
            string trustBundle = $"{TestCertificateHelper.CertificatePem}\n";
            IEnumerable<X509Certificate2> certs = CertificateHelper.ParseTrustedBundleCerts(trustBundle);
            Assert.Single(certs);
        }

        [Fact]
        public void ParseTrustBundleResponseWithMultipleCertReturnsNonEmptyList()
        {
            string trustBundle = $"{TestCertificateHelper.CertificatePem}\n{TestCertificateHelper.CertificatePem}";
            IEnumerable<X509Certificate2> certs = CertificateHelper.ParseTrustedBundleCerts(trustBundle);
            Assert.Equal(2, certs.Count());
        }

        [Fact]
        public void ParseCertificatesSingleShouldReturnCetificate()
        {
            IList<string> pemCerts = CertificateHelper.ParsePemCerts(TestCertificateHelper.CertificatePem);
            IEnumerable<X509Certificate2> certs = CertificateHelper.GetCertificatesFromPem(pemCerts);

            Assert.Single(certs);
        }

        [Fact]
        public void ParseCertificatesMultipleCertsShouldReturnCetificates()
        {
            IList<string> pemCerts = CertificateHelper.ParsePemCerts(TestCertificateHelper.CertificatePem + TestCertificateHelper.CertificatePem);
            IEnumerable<X509Certificate2> certs = CertificateHelper.GetCertificatesFromPem(pemCerts);

            Assert.Equal(2, certs.Count());
        }

        [Fact]
        public void ParseCertificatesWithNonCertificatesEntriesShouldReturnCetificates()
        {
            IList<string> pemCerts = CertificateHelper.ParsePemCerts(TestCertificateHelper.CertificatePem + TestCertificateHelper.CertificatePem + "test");
            IEnumerable<X509Certificate2> certs = CertificateHelper.GetCertificatesFromPem(pemCerts);

            Assert.Equal(2, certs.Count());
        }

        [Fact]
        public void ParseCertificatesNoCertificatesEntriesShouldReturnNoCetificates()
        {
            IList<string> pemCerts = CertificateHelper.ParsePemCerts("test");
            IEnumerable<X509Certificate2> certs = CertificateHelper.GetCertificatesFromPem(pemCerts);

            Assert.Empty(certs);
        }

        [Fact]
        public void ParseCertificatesResponseInvalidCertificateShouldThrow()
        {
            ServerCertificateResponse cert = new ServerCertificateResponse() { Certificate = "InvalidCert" };
            Assert.Throws<InvalidOperationException>(() => CertificateHelper.ParseCertificateResponse(cert));
        }

        [Fact]
        public void ParseCertificatesResponseInvalidKeyShouldThrow()
        {
            var response = new ServerCertificateResponse()
            {
                Certificate = TestCertificateHelper.CertificatePem,
                PrivateKey = "InvalidKey"
            };

            Assert.Throws<InvalidOperationException>(() => CertificateHelper.ParseCertificateResponse(response));
        }

        [Fact]
        public void ParseCertificatesResponseShouldReturnCert()
        {
            TestCertificateHelper.GenerateSelfSignedCert("top secret").Export(X509ContentType.Cert);
            var response = new ServerCertificateResponse()
            {
                Certificate = $"{TestCertificateHelper.CertificatePem}\n{TestCertificateHelper.CertificatePem}",
                PrivateKey = TestCertificateHelper.PrivateKeyPem
            };
            (X509Certificate2 cert, IEnumerable<X509Certificate2> chain) = CertificateHelper.ParseCertificateResponse(response);

            var expected = new X509Certificate2(Encoding.UTF8.GetBytes(TestCertificateHelper.CertificatePem));
            Assert.Equal(expected, cert);
            Assert.True(cert.HasPrivateKey);
            Assert.Single(chain);
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
            Assert.Empty(chain);
        }

        [Fact]
        public void ParseMultipleCertificateAndKeyShouldReturnCertAndKey()
        {
            TestCertificateHelper.GenerateSelfSignedCert("top secret").Export(X509ContentType.Cert);
            string certificate = $"{TestCertificateHelper.CertificatePem}\n{TestCertificateHelper.CertificatePem}";
            (X509Certificate2 cert, IEnumerable<X509Certificate2> chain) = CertificateHelper.ParseCertificateAndKey(certificate, TestCertificateHelper.PrivateKeyPem);

            var expected = new X509Certificate2(Encoding.UTF8.GetBytes(TestCertificateHelper.CertificatePem));
            Assert.Equal(expected, cert);
            Assert.True(cert.HasPrivateKey);
            Assert.Single(chain);
            Assert.Equal(expected, chain.First());
        }

        [Fact]
        public void TestIfCACertificate()
        {
            var notBefore = DateTime.Now.Subtract(TimeSpan.FromDays(2));
            var notAfter = DateTime.Now.AddYears(1);
            var (caCert, caKeyPair) = TestCertificateHelper.GenerateSelfSignedCert("MyTestCA", notBefore, notAfter, true);
            Assert.True(CertificateHelper.IsCACertificate(caCert));

            var (clientCert, clientKeyPair) = TestCertificateHelper.GenerateSelfSignedCert("MyTestClient", notBefore, notAfter, false);
            Assert.False(CertificateHelper.IsCACertificate(clientCert));

            var (issuedClientCert, issuedClientKeyPair) = TestCertificateHelper.GenerateCertificate("MyIssuedTestClient", notBefore, notAfter, caCert, caKeyPair, false, null, null);
            Assert.False(CertificateHelper.IsCACertificate(issuedClientCert));
        }

        [Fact]
        public void TestValidateCertificateWithExpiredValidityFails()
        {
            var notBefore = DateTime.Now.Subtract(TimeSpan.FromDays(2));
            var notAfter = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            var (clientCert, clientKeyPair) = TestCertificateHelper.GenerateSelfSignedCert("MyTestClient", notBefore, notAfter, false);
            Assert.False(CertificateHelper.ValidateClientCert(clientCert, new List<X509Certificate2>() { clientCert }, Option.None<IList<X509Certificate2>>(), Logger.Factory.CreateLogger("something")));
        }

        [Fact]
        public void TestValidateCertificateWithFutureValidityFails()
        {
            var notBefore = DateTime.Now.AddYears(1);
            var notAfter = DateTime.Now.AddYears(2);
            var (clientCert, clientKeyPair) = TestCertificateHelper.GenerateSelfSignedCert("MyTestClient", notBefore, notAfter, false);

            Assert.False(CertificateHelper.ValidateClientCert(clientCert, new List<X509Certificate2>() { clientCert }, Option.None<IList<X509Certificate2>>(), Logger.Factory.CreateLogger("something")));
        }

        [Fact]
        public void TestValidateCertificateWithCAExtentionFails()
        {
            var caCert = TestCertificateHelper.GenerateSelfSignedCert("MyTestCA", true);

            Assert.False(CertificateHelper.ValidateClientCert(caCert, new List<X509Certificate2>() { caCert }, Option.None<IList<X509Certificate2>>(), Logger.Factory.CreateLogger("something")));
        }

        [Fact]
        public void TestValidateCertificateAndChainSucceeds()
        {
            var notBefore = DateTime.Now.Subtract(TimeSpan.FromDays(2));
            var notAfter = DateTime.Now.AddYears(1);
            var (caCert, caKeyPair) = TestCertificateHelper.GenerateSelfSignedCert("MyTestCA", notBefore, notAfter, true);
            var (issuedClientCert, issuedClientKeyPair) = TestCertificateHelper.GenerateCertificate("MyIssuedTestClient", notBefore, notAfter, caCert, caKeyPair, false, null, null);

            Assert.True(CertificateHelper.ValidateClientCert(issuedClientCert, new List<X509Certificate2>() { caCert }, Option.None<IList<X509Certificate2>>(), Logger.Factory.CreateLogger("something")));
        }

        /*TODO need to discuss test failure
        [Fact]
        public void TestValidateCertificateAndChainFails()
        {
            var notBefore = DateTime.Now.Subtract(TimeSpan.FromDays(2));
            var notAfter = DateTime.Now.AddYears(1);
            var (caCert, caKeyPair) = TestCertificateHelper.GenerateSelfSignedCert("MyTestCA", notBefore, notAfter, true);
            var (clientCert, clientKeyPair) = TestCertificateHelper.GenerateSelfSignedCert("MyTestClient", notBefore, notAfter, false);
            var (issuedClientCert, issuedClientKeyPair) = TestCertificateHelper.GenerateCertificate("MyIssuedTestClient", notBefore, notAfter, caCert, caKeyPair, false);

            Assert.False(CertificateHelper.ValidateClientCert(issuedClientCert, new List<X509Certificate2>() { clientCert }, Option.None<IList<X509Certificate2>>(), Logger.Factory.CreateLogger("something")));
        }
        */

        [Fact]
        public void TestValidateTrustedCACertificateAndChainSucceeds()
        {
            var notBefore = DateTime.Now.Subtract(TimeSpan.FromDays(2));
            var notAfter = DateTime.Now.AddYears(1);
            var (caCert, caKeyPair) = TestCertificateHelper.GenerateSelfSignedCert("MyTestCA", notBefore, notAfter, true);
            var (issuedClientCert, issuedClientKeyPair) = TestCertificateHelper.GenerateCertificate("MyIssuedTestClient", notBefore, notAfter, caCert, caKeyPair, false, null, null);
            IList<X509Certificate2> trustedCACerts = new List<X509Certificate2>() { caCert };

            Assert.True(CertificateHelper.ValidateClientCert(issuedClientCert, new List<X509Certificate2>() { caCert }, Option.Some(trustedCACerts), Logger.Factory.CreateLogger("something")));
        }

        [Fact]
        public void TestValidateTrustedCACertificateAndMistmatchChainFails()
        {
            var notBefore = DateTime.Now.Subtract(TimeSpan.FromDays(2));
            var notAfter = DateTime.Now.AddYears(1);
            var (caCert, caKeyPair) = TestCertificateHelper.GenerateSelfSignedCert("MyTestCA", notBefore, notAfter, true);
            var (clientCert, clientKeyPair) = TestCertificateHelper.GenerateSelfSignedCert("MyTestClient", notBefore, notAfter, false);
            var (issuedClientCert, issuedClientKeyPair) = TestCertificateHelper.GenerateCertificate("MyIssuedTestClient", notBefore, notAfter, caCert, caKeyPair, false, null, null);
            IList<X509Certificate2> trustedCACerts = new List<X509Certificate2>() { caCert };

            Assert.False(CertificateHelper.ValidateClientCert(issuedClientCert, new List<X509Certificate2>() { clientCert }, Option.Some(trustedCACerts), Logger.Factory.CreateLogger("something")));
        }

        [Fact]
        public void TestValidateTrustedCACertificateAndEmptyChainFails()
        {
            var notBefore = DateTime.Now.Subtract(TimeSpan.FromDays(2));
            var notAfter = DateTime.Now.AddYears(1);
            var (caCert, caKeyPair) = TestCertificateHelper.GenerateSelfSignedCert("MyTestCA", notBefore, notAfter, true);
            var (issuedClientCert, issuedClientKeyPair) = TestCertificateHelper.GenerateCertificate("MyIssuedTestClient", notBefore, notAfter, caCert, caKeyPair, false, null, null);
            IList<X509Certificate2> trustedCACerts = new List<X509Certificate2>() { caCert };

            Assert.False(CertificateHelper.ValidateClientCert(issuedClientCert, new List<X509Certificate2>() { }, Option.Some(trustedCACerts), Logger.Factory.CreateLogger("something")));
        }
    }
}
