// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class EdgeTlsTransportTest
    {
        [Fact]
        public void TestInvalidConstructorInputs_Fails()
        {
            var tb = new Mock<TransportBase>("TCP");
            var tts = Mock.Of<TlsTransportSettings>();
            var auth = Mock.Of<IAuthenticator>();
            var cf = Mock.Of<IClientCredentialsFactory>();

            Assert.Throws<ArgumentNullException>(() => new EdgeTlsTransport(tb.Object, tts, null, cf));
            Assert.Throws<ArgumentNullException>(() => new EdgeTlsTransport(tb.Object, tts, auth, null));
        }

        [Fact]
        public void TestInvalidConstructorInputs_Succeeds()
        {
            var tb = new Mock<TransportBase>("TCP");
            var tts = Mock.Of<TlsTransportSettings>();
            var auth = Mock.Of<IAuthenticator>();
            var cf = Mock.Of<IClientCredentialsFactory>();

            Assert.NotNull(new EdgeTlsTransport(tb.Object, tts, auth, cf));
        }
    }
}
