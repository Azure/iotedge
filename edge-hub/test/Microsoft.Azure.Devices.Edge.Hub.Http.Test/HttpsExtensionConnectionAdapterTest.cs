// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Adapters;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using TestCertificateHelper = Microsoft.Azure.Devices.Edge.Util.Test.Common.CertificateHelper;

    [Unit]
    public class HttpsExtensionConnectionAdapterTest
    {
        [Fact]
        public void HttpsExtensionConnectionAdapterTestInvalidInput_Fails()
        {
            Assert.Throws<ArgumentNullException>(() => new HttpsExtensionConnectionAdapter(null));
        }

        [Fact]
        public void HttpsExtensionConnectionAdapterTestInvalidOptions_Fails()
        {
            var options = new HttpsConnectionAdapterOptions()
            {
                ServerCertificate = null
            };
            Assert.Throws<ArgumentNullException>(() => new HttpsExtensionConnectionAdapter(options));
        }

        [Fact]
        public void HttpsExtensionConnectionAdapterTestValidCertWithNoEku_Succeeds()
        {
            var cert = TestCertificateHelper.GenerateSelfSignedCert("no eku");
            var options = new HttpsConnectionAdapterOptions()
            {
                ServerCertificate = cert
            };

            Assert.NotNull(new HttpsExtensionConnectionAdapter(options));
        }

        [Fact]
        public void HttpsExtensionConnectionAdapterTestValidCertWithEkuServer_Succeeds()
        {
            var (cert, key) = TestCertificateHelper.GenerateServerCert("eku server auth", DateTime.Now.Subtract(TimeSpan.FromDays(2)), DateTime.Now.AddYears(1));
            var options = new HttpsConnectionAdapterOptions()
            {
                ServerCertificate = cert
            };

            Assert.NotNull(new HttpsExtensionConnectionAdapter(options));
        }

        [Fact]
        public void HttpsExtensionConnectionAdapterTestValidCertWithEkuClient_Fails()
        {
            var (cert, key) = TestCertificateHelper.GenerateClientert("eku client auth", DateTime.Now.Subtract(TimeSpan.FromDays(2)), DateTime.Now.AddYears(1));
            var options = new HttpsConnectionAdapterOptions()
            {
                ServerCertificate = cert
            };

            Assert.Throws<InvalidOperationException>(() => new HttpsExtensionConnectionAdapter(options));
        }

        [Fact]
        public void IsHttpsAlwaysTrue()
        {
            var server = TestCertificateHelper.GenerateSelfSignedCert("test server");
            var options = new HttpsConnectionAdapterOptions()
            {
                ServerCertificate = server
            };
            var ext = new HttpsExtensionConnectionAdapter(options);
            Assert.True(ext.IsHttps);
        }
    }
}
