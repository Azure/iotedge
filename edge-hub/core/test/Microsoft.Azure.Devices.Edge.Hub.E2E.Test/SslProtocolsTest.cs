// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.SslProtocolsTest")]
    public class SslProtocolsTest : IDisposable
    {
        readonly ProtocolHeadFixture protocolHead;

        public SslProtocolsTest()
        {
            this.protocolHead = EdgeHubFixtureCollection.GetFixture(SslProtocols.Tls13);
        }

        public async void Dispose() => await (this.protocolHead?.CloseAsync() ?? Task.CompletedTask);

        [Fact]
        public void SslProtocolConnectionTest()
        {
            int httpsPort = 443;
            int amqpsPort = 5671;
            int mqttPort = 8883;
            SslProtocols validProtocol = SslProtocols.Tls13;
            SslProtocols invalidProtocol = SslProtocols.Tls12;
            Option<Type> expectedException = Option.Some(typeof(AuthenticationException));

            TestConnection(httpsPort, validProtocol, Option.None<Type>());
            TestConnection(amqpsPort, validProtocol, Option.None<Type>());
            TestConnection(mqttPort, validProtocol, Option.None<Type>());

            TestConnection(httpsPort, invalidProtocol, expectedException);
            TestConnection(amqpsPort, invalidProtocol, expectedException);
            TestConnection(mqttPort, invalidProtocol, expectedException);
        }

        static void TestConnection(int port, SslProtocols protocol, Option<Type> expectedException)
        {
            string server = "127.0.0.1";
            TcpClient client = new TcpClient(server, port);
            Exception exception = null;

            using (SslStream sslStream = new SslStream(
                client.GetStream(),
                false,
                (sender, certificate, chain, errors) => true,
                null))
            {
                try
                {
                    sslStream.AuthenticateAsClient(server, null, protocol, false);
                }
                catch (Exception e)
                {
                    exception = e;
                }
            }

            client.Close();

            if (!expectedException.HasValue)
            {
                Assert.Null(exception);
            }
            else
            {
                Assert.NotNull(exception);
                expectedException.ForEach(e => Assert.Equal(e, exception.GetType()));
            }
        }
    }
}
