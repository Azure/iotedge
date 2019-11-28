// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
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
    public class SslProtocolsTest
    {
        [Fact]
        public void DefaultSslProtocolsTest()
        {
            using (IDisposable protocolHead = EdgeHubFixtureCollection.GetFixture(SslProtocols.Tls12))
            {
                //ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) => true;
                //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11;
                //Uri uri = new Uri("https://127.0.0.1:433");
                //WebRequest webRequest = WebRequest.Create(uri);
                //WebResponse webResponse = webRequest.GetResponse();

                string server = "127.0.0.1";
                TcpClient client = new TcpClient(server, 443);

                using (SslStream sslStream = new SslStream(client.GetStream(), false,
                                                           new RemoteCertificateValidationCallback(ValidateServerCertificate), null))
                {
                    sslStream.AuthenticateAsClient(server, null, SslProtocols.Tls11, false);
                    // This is where you read and send data
                }
                client.Close();
            }
        }

        public static bool ValidateServerCertificate(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
