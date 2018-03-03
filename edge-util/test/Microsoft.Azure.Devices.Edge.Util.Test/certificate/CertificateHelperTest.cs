// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Certificate
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using TestCertificateHelper = Microsoft.Azure.Devices.Edge.Util.Test.Common.CertificateHelper;
    using CertificateHelper = CertificateHelper.CertificateHelper;
    using System.Security.Cryptography.X509Certificates;

    [Unit]
    public class CertificateHelperTest
    {
        [Fact]
        public void GetThumbprintNullCertThrows()
        {
            Assert.Throws<ArgumentNullException>(() => CertificateHelper.GetSHA256Thumbprint(null));
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
            (bool validated, Option<string> errors) = CertificateHelper.ValidateCert(cert, new X509Certificate2[] { cert }, new X509Certificate2[] { cert });
            Assert.True(validated);
            Assert.False(errors.HasValue);
        }

        [Fact]
        public void ValidateCertNoMatchFailure()
        {
            X509Certificate2 cert = TestCertificateHelper.GenerateSelfSignedCert("top secret");
            X509Certificate2 root = TestCertificateHelper.GenerateSelfSignedCert("root");
            (bool validated, Option<string> errors) = CertificateHelper.ValidateCert(cert, new X509Certificate2[] { cert }, new X509Certificate2[] { root });
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
        public void ClientCertCallbackNoCACertsFails()
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
    }
}
