// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Integration]
    [Collection("Microsoft.Azure.Devices.Edge.Hub.E2E.SslProtocolsTest")]
    public class SslProtocolsTest : IDisposable
    {
        readonly IDisposable protocolHead;

        public SslProtocolsTest()
        {
            this.protocolHead = EdgeHubFixtureCollection.GetFixture(SslProtocols.Tls12);
        }

        public void Dispose()
        {
            this.protocolHead?.Dispose();
        }

        [Fact]
        public void SslProtocolConnectionTest()
        {
            int httpsPort = 443;
            int amqpsPort = 5671;
            int mqttPort = 8883;
            SslProtocols validProtocol = SslProtocols.Tls12;
            SslProtocols invalidProtocol1 = SslProtocols.Tls;
            SslProtocols invalidProtocol2 = SslProtocols.Tls11;
            Type expectedException = typeof(IOException);

            TestConnection(httpsPort, validProtocol, null);
            TestConnection(amqpsPort, validProtocol, null);
            TestConnection(mqttPort, validProtocol, null);

            TestConnection(httpsPort, invalidProtocol1, expectedException);
            TestConnection(amqpsPort, invalidProtocol1, expectedException);
            TestConnection(mqttPort, invalidProtocol1, expectedException);

            TestConnection(httpsPort, invalidProtocol2, expectedException);
            TestConnection(amqpsPort, invalidProtocol2, expectedException);
            TestConnection(mqttPort, invalidProtocol2, expectedException);

        }

        static void TestConnection(int port, SslProtocols protocol, Type expectedException)
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

            if (expectedException == null)
            {
                Assert.Null(exception);
            }
            else
            {
                Assert.NotNull(exception);
                Assert.Equal(expectedException, exception.GetType());
            }
        }
    }
}
