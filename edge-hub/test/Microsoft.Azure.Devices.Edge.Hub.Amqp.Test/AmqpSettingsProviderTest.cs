// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class AmqpSettingsProviderTest
    {
        [Fact]
        [Unit]
        public void TestInvalidInputsForGetDefaultAmqpSettings()
        {
            const string HostName = "edge.ms.com";
            const string IotHubHostName = "restaurantatendofuniverse.azure-devices.net";
            var authenticator = new Mock<IAuthenticator>();
            var identityFactory = new Mock<IClientCredentialsFactory>();
            var linkHandlerProvider = Mock.Of<ILinkHandlerProvider>();
            var connectionProvider = Mock.Of<IConnectionProvider>();
            X509Certificate2 tlsCertificate = CertificateHelper.GenerateSelfSignedCert("TestCert");

            Assert.Throws<ArgumentException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(null, IotHubHostName, tlsCertificate, authenticator.Object, identityFactory.Object, linkHandlerProvider, connectionProvider));
            Assert.Throws<ArgumentException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings("", IotHubHostName, tlsCertificate, authenticator.Object, identityFactory.Object, linkHandlerProvider, connectionProvider));
            Assert.Throws<ArgumentException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings("    ", IotHubHostName, tlsCertificate, authenticator.Object, identityFactory.Object, linkHandlerProvider, connectionProvider));
            Assert.Throws<ArgumentNullException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(HostName, IotHubHostName, null, authenticator.Object, identityFactory.Object, linkHandlerProvider, connectionProvider));
            Assert.Throws<ArgumentNullException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(HostName, IotHubHostName, tlsCertificate, null, identityFactory.Object, linkHandlerProvider, connectionProvider));
            Assert.Throws<ArgumentNullException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(HostName, IotHubHostName, tlsCertificate, authenticator.Object, null, linkHandlerProvider, connectionProvider));

            Assert.NotNull(AmqpSettingsProvider.GetDefaultAmqpSettings(HostName, IotHubHostName, tlsCertificate, authenticator.Object, identityFactory.Object, linkHandlerProvider, connectionProvider));
        }
    }
}
