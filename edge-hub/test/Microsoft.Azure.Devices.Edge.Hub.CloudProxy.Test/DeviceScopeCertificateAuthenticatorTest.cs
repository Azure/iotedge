// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Authenticators;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using TestCertificateHelper = Microsoft.Azure.Devices.Edge.Util.Test.Common.CertificateHelper;
    using Moq;
    using Xunit;
    
    [Unit]
    public class DeviceScopeCertificateAuthenticatorTest
    {
        string iothubHostName = "testiothub.azure-devices.net";
        IAuthenticator underlyingAuthenticator = new NullAuthenticator();

        [Fact]
        public void DeviceScopeCertificateAuthenticatorNullArgumentsThrows()
        {
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();
            IList<X509Certificate2> trustBundle = new List<X509Certificate2>();

            Assert.Throws<ArgumentNullException>(() => new DeviceScopeCertificateAuthenticator(null, iothubHostName, underlyingAuthenticator, trustBundle));
            Assert.Throws<ArgumentException>(() => new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache, null, underlyingAuthenticator, trustBundle));
            Assert.Throws<ArgumentException>(() => new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache, "", underlyingAuthenticator, trustBundle));
            Assert.Throws<ArgumentException>(() => new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache, "   ", underlyingAuthenticator, trustBundle));
            Assert.Throws<ArgumentNullException>(() => new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache, iothubHostName, null, trustBundle));
            Assert.Throws<ArgumentNullException>(() => new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache, iothubHostName, underlyingAuthenticator, null));
        }

        [Fact]
        public async Task AuthenticateAsyncWithNonX509CredsFails()
        {
            var deviceScopeIdentitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>();
            IList<X509Certificate2> trustBundle = new List<X509Certificate2>();
            IClientCredentials clientCredentials = Mock.Of<IClientCredentials>();
            var authenticator = new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache, iothubHostName, underlyingAuthenticator, trustBundle);

            Assert.False(await authenticator.AuthenticateAsync(clientCredentials));
        }

        [Fact]
        public async Task AuthenticateAsyncWithDeviceThumbprintX509InScopeCacheSucceeds()
        {
            string deviceId = "d1";
            var primaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("primo");
            var primaryClientCertChain = new List<X509Certificate2>() { primaryCertificate };
            var secondaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("secondo");
            var secondaryClientCertChain = new List<X509Certificate2>() { secondaryCertificate };

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            IList<X509Certificate2> trustBundle = new List<X509Certificate2>();
            var primaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IDeviceIdentity>(i => i.DeviceId == deviceId && i.Id == deviceId)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == primaryCertificate && c.ClientCertificateChain == primaryClientCertChain);

            var secondaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IDeviceIdentity>(i => i.DeviceId == deviceId && i.Id == deviceId)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == secondaryCertificate && c.ClientCertificateChain == secondaryClientCertChain);

            var serviceIdentity = new ServiceIdentity(deviceId, "1234", new string[0],
                                                      new ServiceAuthentication(new X509ThumbprintAuthentication(primaryCertificate.Thumbprint, secondaryCertificate.Thumbprint)),
                                                      ServiceIdentityStatus.Enabled);
            var authenticator = new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, underlyingAuthenticator, trustBundle);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceId), false)).ReturnsAsync(Option.Some(serviceIdentity));

            // Assert
            Assert.True(await authenticator.AuthenticateAsync(primaryCredentials));
            Assert.True(await authenticator.AuthenticateAsync(secondaryCredentials));
        }

        [Fact]
        public async Task AuthenticateAsyncWithDeviceThumbprintX509MismatchInScopeCacheFails()
        {
            string deviceId = "d1";
            var primaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("primo");
            var primaryClientCertChain = new List<X509Certificate2>() { primaryCertificate };
            var secondaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("secondo");
            var secondaryClientCertChain = new List<X509Certificate2>() { secondaryCertificate };

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            IList<X509Certificate2> trustBundle = new List<X509Certificate2>();
            var primaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IDeviceIdentity>(i => i.DeviceId == deviceId && i.Id == deviceId)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == primaryCertificate && c.ClientCertificateChain == primaryClientCertChain);

            var secondaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IDeviceIdentity>(i => i.DeviceId == deviceId && i.Id == deviceId)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == secondaryCertificate && c.ClientCertificateChain == secondaryClientCertChain);

            var serviceIdentity = new ServiceIdentity(deviceId, "1234", new string[0],
                                                      new ServiceAuthentication(new X509ThumbprintAuthentication("7A57E1E55", "DECAF")),
                                                      ServiceIdentityStatus.Enabled);
            var authenticator = new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, underlyingAuthenticator, trustBundle);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceId), false)).ReturnsAsync(Option.Some(serviceIdentity));

            // Assert
            Assert.False(await authenticator.AuthenticateAsync(primaryCredentials));
            Assert.False(await authenticator.AuthenticateAsync(secondaryCredentials));
        }

        [Fact]
        public async Task AuthenticateAsyncWithDeviceThumbprintX509NotInScopeCacheFails()
        {
            string deviceId = "d1";
            var primaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("primo");
            var primaryClientCertChain = new List<X509Certificate2>() { primaryCertificate };
            var secondaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("secondo");
            var secondaryClientCertChain = new List<X509Certificate2>() { secondaryCertificate };

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            IList<X509Certificate2> trustBundle = new List<X509Certificate2>();
            var primaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IDeviceIdentity>(i => i.DeviceId == deviceId && i.Id == deviceId)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == primaryCertificate && c.ClientCertificateChain == primaryClientCertChain);

            var secondaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IDeviceIdentity>(i => i.DeviceId == deviceId && i.Id == deviceId)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == secondaryCertificate && c.ClientCertificateChain == secondaryClientCertChain);

            // setup identity for another device id
            var serviceIdentity = new ServiceIdentity("some_other_device", "1234", new string[0],
                                                      new ServiceAuthentication(new X509ThumbprintAuthentication(primaryCertificate.Thumbprint, secondaryCertificate.Thumbprint)),
                                                      ServiceIdentityStatus.Enabled);
            var authenticator = new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, underlyingAuthenticator, trustBundle);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == "some_other_device"), false)).ReturnsAsync(Option.Some(serviceIdentity));

            // Assert
            Assert.False(await authenticator.AuthenticateAsync(primaryCredentials));
            Assert.False(await authenticator.AuthenticateAsync(secondaryCredentials));
        }

        [Fact]
        public async Task AuthenticateAsyncWithDeviceThumbprintX509InconsistentAuthInScopeCacheFails()
        {
            string deviceId = "d1";
            var primaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("primo");
            var primaryClientCertChain = new List<X509Certificate2>() { primaryCertificate };
            var secondaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("secondo");
            var secondaryClientCertChain = new List<X509Certificate2>() { secondaryCertificate };

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            IList<X509Certificate2> trustBundle = new List<X509Certificate2>();
            var primaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IDeviceIdentity>(i => i.DeviceId == deviceId && i.Id == deviceId)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == primaryCertificate && c.ClientCertificateChain == primaryClientCertChain);

            var secondaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IDeviceIdentity>(i => i.DeviceId == deviceId && i.Id == deviceId)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == secondaryCertificate && c.ClientCertificateChain == secondaryClientCertChain);

            string key = GetKey();
            var serviceIdentity = new ServiceIdentity(deviceId, "1234", new string[0],
                                                      new ServiceAuthentication(new SymmetricKeyAuthentication(key, GetKey())), ServiceIdentityStatus.Enabled);

            var authenticator = new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, underlyingAuthenticator, trustBundle);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == deviceId), false)).ReturnsAsync(Option.Some(serviceIdentity));

            // Assert
            Assert.False(await authenticator.AuthenticateAsync(secondaryCredentials));
        }

        [Fact]
        public async Task AuthenticateAsyncWithModuleThumbprintX509InScopeCacheSucceeds()
        {
            string deviceId = "d1";
            string moduleId = "m1";
            string identity = FormattableString.Invariant($"{deviceId}/{moduleId}");
            var primaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("primo");
            var primaryClientCertChain = new List<X509Certificate2>() { primaryCertificate };
            var secondaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("secondo");
            var secondaryClientCertChain = new List<X509Certificate2>() { secondaryCertificate };

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            IList<X509Certificate2> trustBundle = new List<X509Certificate2>();
            var primaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IModuleIdentity>(i => i.DeviceId == deviceId && i.ModuleId == moduleId
                    && i.Id == identity)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == primaryCertificate && c.ClientCertificateChain == primaryClientCertChain);

            var secondaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IModuleIdentity>(i => i.DeviceId == deviceId && i.ModuleId == moduleId
                    && i.Id == identity)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == secondaryCertificate && c.ClientCertificateChain == secondaryClientCertChain);

            var serviceIdentity = new ServiceIdentity(deviceId, moduleId, "1234", new string[0],
                                                      new ServiceAuthentication(new X509ThumbprintAuthentication(primaryCertificate.Thumbprint, secondaryCertificate.Thumbprint)),
                                                      ServiceIdentityStatus.Enabled);
            var authenticator = new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, underlyingAuthenticator, trustBundle);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == identity), false)).ReturnsAsync(Option.Some(serviceIdentity));

            // Assert
            Assert.True(await authenticator.AuthenticateAsync(primaryCredentials));
            Assert.True(await authenticator.AuthenticateAsync(secondaryCredentials));
        }

        [Fact]
        public async Task AuthenticateAsyncWithModuleThumbprintX509NotInScopeCacheFails()
        {
            string deviceId = "d1";
            string moduleId = "m1";
            string identity = FormattableString.Invariant($"{deviceId}/{moduleId}");
            var primaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("primo");
            var primaryClientCertChain = new List<X509Certificate2>() { primaryCertificate };
            var secondaryCertificate = TestCertificateHelper.GenerateSelfSignedCert("secondo");
            var secondaryClientCertChain = new List<X509Certificate2>() { secondaryCertificate };

            var deviceScopeIdentitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            IList<X509Certificate2> trustBundle = new List<X509Certificate2>();
            var primaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IModuleIdentity>(i => i.DeviceId == deviceId && i.ModuleId == moduleId
                    && i.Id == identity)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == primaryCertificate && c.ClientCertificateChain == primaryClientCertChain);

            var secondaryCredentials = Mock.Of<ICertificateCredentials>(c =>
                c.Identity == Mock.Of<IModuleIdentity>(i => i.DeviceId == deviceId && i.ModuleId == moduleId
                    && i.Id == identity)
                    && c.AuthenticationType == AuthenticationType.X509Cert
                    && c.ClientCertificate == secondaryCertificate && c.ClientCertificateChain == secondaryClientCertChain);

            var serviceIdentity = new ServiceIdentity(deviceId, moduleId, "1234", new string[0],
                                                      new ServiceAuthentication(new X509ThumbprintAuthentication("7A57E1E55", "DECAF")),
                                                      ServiceIdentityStatus.Enabled);
            var authenticator = new DeviceScopeCertificateAuthenticator(deviceScopeIdentitiesCache.Object, iothubHostName, underlyingAuthenticator, trustBundle);
            deviceScopeIdentitiesCache.Setup(d => d.GetServiceIdentity(It.Is<string>(i => i == identity), false)).ReturnsAsync(Option.Some(serviceIdentity));

            // Assert
            Assert.False(await authenticator.AuthenticateAsync(primaryCredentials));
            Assert.False(await authenticator.AuthenticateAsync(secondaryCredentials));
        }

        static string GetKey() => Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
    }
}
