// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class DefaultTransportSettingsTest
    {
        [Fact]
        [Unit]
        public void TestInvalidConstructorInputs()
        {
            const string Scheme = "amqps";
            const string HostName = "restaurantatendofuniverse.azure-devices.net";
            const int Port = 5671;
            const bool clientCertsAllowed = true;
            X509Certificate2 tlsCertificate = CertificateHelper.GenerateSelfSignedCert("TestCert");
            var credentialsProvider = Mock.Of<IClientCredentialsFactory>();
            var autheticator = Mock.Of<IAuthenticator>();

            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings(null, HostName, Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider, SslProtocols.Tls12));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings(string.Empty, HostName, Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider, SslProtocols.Tls12));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings("    ", HostName, Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider, SslProtocols.Tls12));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings(Scheme, null, Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider, SslProtocols.Tls12));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings(Scheme, string.Empty, Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider, SslProtocols.Tls12));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings(Scheme, "   ", Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider, SslProtocols.Tls12));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DefaultTransportSettings(Scheme, HostName, -1, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider, SslProtocols.Tls12));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DefaultTransportSettings(Scheme, HostName, 70000, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider, SslProtocols.Tls12));
            Assert.Throws<ArgumentNullException>(() => new DefaultTransportSettings(Scheme, HostName, Port, null, clientCertsAllowed, autheticator, credentialsProvider, SslProtocols.Tls12));
            Assert.Throws<ArgumentNullException>(() => new DefaultTransportSettings(Scheme, HostName, Port, tlsCertificate, clientCertsAllowed, null, credentialsProvider, SslProtocols.Tls12));
            Assert.Throws<ArgumentNullException>(() => new DefaultTransportSettings(Scheme, HostName, Port, tlsCertificate, clientCertsAllowed, autheticator, null, SslProtocols.Tls12));

            Assert.NotNull(new DefaultTransportSettings(Scheme, HostName, Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider, SslProtocols.Tls12));
            Assert.NotNull(new DefaultTransportSettings(Scheme, HostName, Port, tlsCertificate, !clientCertsAllowed, autheticator, credentialsProvider, SslProtocols.Tls12));
        }
    }
}
