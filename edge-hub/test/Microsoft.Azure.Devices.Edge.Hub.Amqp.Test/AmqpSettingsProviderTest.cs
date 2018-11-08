// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;

    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Sasl;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Moq;

    using Xunit;

    using Constants = Microsoft.Azure.Devices.Edge.Hub.Amqp.Constants;

    [Unit]
    public class AmqpSettingsProviderTest
    {
        [Fact]
        public void TestInvalidInputsForGetDefaultAmqpSettings()
        {
            const string IotHubHostName = "restaurantatendofuniverse.azure-devices.net";
            var authenticator = new Mock<IAuthenticator>();
            var identityFactory = new Mock<IClientCredentialsFactory>();
            var linkHandlerProvider = Mock.Of<ILinkHandlerProvider>();
            var connectionProvider = Mock.Of<IConnectionProvider>();

            Assert.Throws<ArgumentException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(null, authenticator.Object, identityFactory.Object, linkHandlerProvider, connectionProvider, new NullCredentialsCache()));
            Assert.Throws<ArgumentNullException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, null, identityFactory.Object, linkHandlerProvider, connectionProvider, new NullCredentialsCache()));
            Assert.Throws<ArgumentNullException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, authenticator.Object, null, linkHandlerProvider, connectionProvider, new NullCredentialsCache()));

            Assert.NotNull(AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, authenticator.Object, identityFactory.Object, linkHandlerProvider, connectionProvider, new NullCredentialsCache()));
        }

        [Fact]
        public void ValidateSettingsTest()
        {
            // Arrange
            string iotHubHostName = "foo.azure-devices.net";
            var authenticator = Mock.Of<IAuthenticator>();
            var identityFactory = Mock.Of<IClientCredentialsFactory>();
            var linkHandlerProvider = Mock.Of<ILinkHandlerProvider>();
            var connectionProvider = Mock.Of<IConnectionProvider>();

            // Act
            AmqpSettings settings = AmqpSettingsProvider.GetDefaultAmqpSettings(iotHubHostName, authenticator, identityFactory, linkHandlerProvider, connectionProvider, new NullCredentialsCache());

            // Assert
            Assert.NotNull(settings);
            Assert.Equal(2, settings.TransportProviders.Count);

            var saslTransportProvider = settings.GetTransportProvider<SaslTransportProvider>();
            Assert.NotNull(saslTransportProvider);

            SaslHandler anonHandler = saslTransportProvider.GetHandler("ANONYMOUS", false);
            Assert.NotNull(anonHandler);

            SaslHandler plainHandler = saslTransportProvider.GetHandler("PLAIN", false);
            Assert.NotNull(plainHandler);

            SaslHandler cbsHandler = saslTransportProvider.GetHandler(Constants.ServiceBusCbsSaslMechanismName, false);
            Assert.NotNull(cbsHandler);

            var amqpTransportProvider = settings.GetTransportProvider<AmqpTransportProvider>();
            Assert.NotNull(amqpTransportProvider);

            Assert.Equal(Constants.AmqpVersion100, amqpTransportProvider.Versions[0]);
        }
    }
}
