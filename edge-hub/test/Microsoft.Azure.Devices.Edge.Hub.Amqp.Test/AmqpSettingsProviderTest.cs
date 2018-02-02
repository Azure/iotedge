// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Security.Cryptography.X509Certificates;
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
            const string HostName = "restaurantatendofuniverse.azure-devices.net";
            var authenticator = new Mock<IAuthenticator>();
            var identityFactory = new Mock<IIdentityFactory>();
            X509Certificate2 tlsCertificate = CertificateHelper.GenerateSelfSignedCert("TestCert");

            Assert.Throws<ArgumentException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(null, tlsCertificate, authenticator.Object, identityFactory.Object));
            Assert.Throws<ArgumentException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings("", tlsCertificate, authenticator.Object, identityFactory.Object));
            Assert.Throws<ArgumentException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings("    ", tlsCertificate, authenticator.Object, identityFactory.Object));
            Assert.Throws<ArgumentNullException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(HostName, null, authenticator.Object, identityFactory.Object));
            Assert.Throws<ArgumentNullException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(HostName, tlsCertificate, null, identityFactory.Object));
            Assert.Throws<ArgumentNullException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(HostName, tlsCertificate, authenticator.Object, null));

            Assert.NotNull(AmqpSettingsProvider.GetDefaultAmqpSettings(HostName, tlsCertificate, authenticator.Object, identityFactory.Object));
        }
    }
}
