// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using Moq;

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


            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings(null, HostName, Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings("", HostName, Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings("    ", HostName, Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings(Scheme, null, Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings(Scheme, "", Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings(Scheme, "   ", Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DefaultTransportSettings(Scheme, HostName, -1, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DefaultTransportSettings(Scheme, HostName, 70000, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider));
            Assert.Throws<ArgumentNullException>(() => new DefaultTransportSettings(Scheme, HostName, Port, null, clientCertsAllowed, autheticator, credentialsProvider));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings(Scheme, HostName, Port, tlsCertificate, clientCertsAllowed, null, credentialsProvider));
            Assert.Throws<ArgumentException>(() => new DefaultTransportSettings(Scheme, HostName, Port, tlsCertificate, clientCertsAllowed, autheticator, null));

            Assert.NotNull(new DefaultTransportSettings(Scheme, HostName, Port, tlsCertificate, clientCertsAllowed, autheticator, credentialsProvider));
            Assert.NotNull(new DefaultTransportSettings(Scheme, HostName, Port, tlsCertificate, !clientCertsAllowed, autheticator, credentialsProvider));
        }
    }
}
