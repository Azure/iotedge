// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
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
            const string IotHubHostName = "restaurantatendofuniverse.azure-devices.net";
            var authenticator = new Mock<IAuthenticator>();
            var identityFactory = new Mock<IClientCredentialsFactory>();
            var linkHandlerProvider = Mock.Of<ILinkHandlerProvider>();
            var connectionProvider = Mock.Of<IConnectionProvider>();

            Assert.Throws<ArgumentException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(null, authenticator.Object, identityFactory.Object, linkHandlerProvider, connectionProvider));
            Assert.Throws<ArgumentNullException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, null, identityFactory.Object, linkHandlerProvider, connectionProvider));
            Assert.Throws<ArgumentNullException>(() => AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, authenticator.Object, null, linkHandlerProvider, connectionProvider));

            Assert.NotNull(AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, authenticator.Object, identityFactory.Object, linkHandlerProvider, connectionProvider));
        }
    }
}
