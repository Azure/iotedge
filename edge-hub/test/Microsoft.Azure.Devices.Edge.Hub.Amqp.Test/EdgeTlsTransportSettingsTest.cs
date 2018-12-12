// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Moq;
    using Xunit;

    public class EdgeTlsTransportSettingsTest
    {
        [Fact]
        public void TestInvalidConstructorInputs_Fails()
        {
            var transportSettings = Mock.Of<TlsTransportSettings>();
            var auth = Mock.Of<IAuthenticator>();
            var cf = Mock.Of<IClientCredentialsFactory>();

            Assert.Throws<ArgumentNullException>(() => new EdgeTlsTransportSettings(transportSettings, false, null, cf));
            Assert.Throws<ArgumentNullException>(() => new EdgeTlsTransportSettings(transportSettings, false, auth, null));
        }

        [Fact]
        public void TestValidConstructorInputs_Succeeds()
        {
            var transportSettings = Mock.Of<TlsTransportSettings>();
            var auth = Mock.Of<IAuthenticator>();
            var cf = Mock.Of<IClientCredentialsFactory>();

            Assert.NotNull(new EdgeTlsTransportSettings(transportSettings, false, auth, cf));
        }
    }
}
