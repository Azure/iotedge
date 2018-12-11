// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Amqp.X509;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using TestCertificateHelper = Microsoft.Azure.Devices.Edge.Util.Test.Common.CertificateHelper;
    using Moq;
    using Xunit;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    [Unit]
    public class EdgeX509PrincipalTest
    {
        [Fact]
        public void TestInvalidConstructorInputs_Fails()
        {
            var certificate = TestCertificateHelper.GenerateSelfSignedCert("test moi");
            var chain = new List<X509Certificate2>() { certificate };
            var identity = new X509CertificateIdentity(certificate, true);
            var auth = Mock.Of<IAuthenticator>();
            var cf = Mock.Of<IClientCredentialsFactory>();

            Assert.Throws<ArgumentNullException>(() => new EdgeX509Principal(identity, null, auth, cf));
            Assert.Throws<ArgumentNullException>(() => new EdgeX509Principal(identity, chain, null, cf));
            Assert.Throws<ArgumentNullException>(() => new EdgeX509Principal(identity, chain, auth, null));
        }

        [Fact]
        public void TestValidConstructorInputs_Succeeds()
        {
            var certificate = TestCertificateHelper.GenerateSelfSignedCert("test moi");
            var chain = new List<X509Certificate2>() { certificate };
            var identity = new X509CertificateIdentity(certificate, true);
            var auth = Mock.Of<IAuthenticator>();
            var cf = Mock.Of<IClientCredentialsFactory>();

            Assert.NotNull(new EdgeX509Principal(identity, chain, auth, cf));
        }

        [Fact]
        public async Task TestAuthenticateAsyncWithInvalidId_FailsAsync()
        {
            var certificate = TestCertificateHelper.GenerateSelfSignedCert("test moi");
            var chain = new List<X509Certificate2>() { certificate };
            var identity = new X509CertificateIdentity(certificate, true);
            var auth = Mock.Of<IAuthenticator>();
            var cf = Mock.Of<IClientCredentialsFactory>();

            var principal = new EdgeX509Principal(identity, chain, auth, cf);
            await Assert.ThrowsAsync<ArgumentException>(() => principal.AuthenticateAsync(null));
            await Assert.ThrowsAsync<ArgumentException>(() => principal.AuthenticateAsync(""));
            await Assert.ThrowsAsync<ArgumentException>(() => principal.AuthenticateAsync("   "));
            Assert.False(await principal.AuthenticateAsync("/   "));
            Assert.False(await principal.AuthenticateAsync("   /"));
            Assert.False(await principal.AuthenticateAsync("   /   "));
            Assert.False(await principal.AuthenticateAsync("did/"));
            Assert.False(await principal.AuthenticateAsync("did/   "));
            Assert.False(await principal.AuthenticateAsync("/mid"));
            Assert.False(await principal.AuthenticateAsync("   /mid"));
            Assert.False(await principal.AuthenticateAsync("did/mid/blah"));
        }
    
        [Fact]
        public async Task TestAsyncAuthentionReturnsFalse_FailsAsync()
        {
            var certificate = TestCertificateHelper.GenerateSelfSignedCert("test moi");
            var chain = new List<X509Certificate2>() { certificate };
            var identity = new X509CertificateIdentity(certificate, true);
            var authenticator = Mock.Of<IAuthenticator>();
            var clientCredentialsFactory = Mock.Of<IClientCredentialsFactory>();
            var clientCredentials = Mock.Of<IClientCredentials>();
            var principal = new EdgeX509Principal(identity, chain, authenticator, clientCredentialsFactory);
            string deviceId = "myDid";
            string moduleId = "myMid";

            Mock.Get(clientCredentialsFactory).Setup(f => f.GetWithX509Cert(deviceId,
                                                                            moduleId,
                                                                            string.Empty,
                                                                            certificate,
                                                                            chain))
                                                                            .Returns(clientCredentials);
            Mock.Get(authenticator).Setup(a => a.AuthenticateAsync(clientCredentials)).ReturnsAsync(false);

            Assert.False(await principal.AuthenticateAsync($"{deviceId}/{moduleId}"));
        }

        [Fact]
        public async Task TestAsyncAuthentionDeviceIdReturnsTrus_SucceedsAsync()
        {
            var certificate = TestCertificateHelper.GenerateSelfSignedCert("test moi");
            var chain = new List<X509Certificate2>() { certificate };
            var identity = new X509CertificateIdentity(certificate, true);
            var authenticator = Mock.Of<IAuthenticator>();
            var clientCredentialsFactory = Mock.Of<IClientCredentialsFactory>();
            var clientCredentials = Mock.Of<IClientCredentials>();
            var principal = new EdgeX509Principal(identity, chain, authenticator, clientCredentialsFactory);
            string deviceId = "myDid";
            string moduleId = string.Empty;

            Mock.Get(clientCredentialsFactory).Setup(f => f.GetWithX509Cert(deviceId,
                                                                            moduleId,
                                                                            string.Empty,
                                                                            certificate,
                                                                            chain))
                                                                            .Returns(clientCredentials);
            Mock.Get(authenticator).Setup(a => a.AuthenticateAsync(clientCredentials)).ReturnsAsync(true);

            Assert.True(await principal.AuthenticateAsync($"{deviceId}"));
        }

        [Fact]
        public async Task TestAsyncAuthentionModuleIdReturnsTrus_SucceedsAsync()
        {
            var certificate = TestCertificateHelper.GenerateSelfSignedCert("test moi");
            var chain = new List<X509Certificate2>() { certificate };
            var identity = new X509CertificateIdentity(certificate, true);
            var authenticator = Mock.Of<IAuthenticator>();
            var clientCredentialsFactory = Mock.Of<IClientCredentialsFactory>();
            var clientCredentials = Mock.Of<IClientCredentials>();
            var principal = new EdgeX509Principal(identity, chain, authenticator, clientCredentialsFactory);
            string deviceId = "myDid";
            string moduleId = "myMid";

            Mock.Get(clientCredentialsFactory).Setup(f => f.GetWithX509Cert(deviceId,
                                                                            moduleId,
                                                                            string.Empty,
                                                                            certificate,
                                                                            chain))
                                                                            .Returns(clientCredentials);
            Mock.Get(authenticator).Setup(a => a.AuthenticateAsync(clientCredentials)).ReturnsAsync(true);

            Assert.True(await principal.AuthenticateAsync($"{deviceId}/{moduleId}"));
        }
    }
}
