// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class AmqpProtocolHeadTest
    {
        const string Scheme = "amqps";
        const string HostName = "localhost";
        const string IotHubHostName = "restaurantatendofuniverse.azure-devices.net";
        const int Port = 5671;

        [Fact]
        [Unit]
        public void TestInvalidConstructorInputs()
        {
            const bool clientCertsAllowed = true;
            X509Certificate2 tlsCertificate = CertificateHelper.GenerateSelfSignedCert("TestCert");
            var transportSettings = new DefaultTransportSettings(Scheme, HostName, Port, tlsCertificate, clientCertsAllowed, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>(), SslProtocols.Tls12);
            AmqpSettings amqpSettings = AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>(), Mock.Of<ILinkHandlerProvider>(), Mock.Of<IConnectionProvider>(), new NullCredentialsCache());
            var transportListenerProvider = new Mock<ITransportListenerProvider>();
            var webSockerListenerRegistry = new Mock<IWebSocketListenerRegistry>();

            Assert.Throws<ArgumentNullException>(() => new AmqpProtocolHead(null, amqpSettings, transportListenerProvider.Object, webSockerListenerRegistry.Object, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>()));
            Assert.Throws<ArgumentNullException>(() => new AmqpProtocolHead(transportSettings, null, transportListenerProvider.Object, webSockerListenerRegistry.Object, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>()));
            Assert.Throws<ArgumentNullException>(() => new AmqpProtocolHead(transportSettings, amqpSettings, null, webSockerListenerRegistry.Object, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>()));
            Assert.Throws<ArgumentNullException>(() => new AmqpProtocolHead(transportSettings, amqpSettings, transportListenerProvider.Object, null, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>()));
            Assert.Throws<ArgumentNullException>(() => new AmqpProtocolHead(transportSettings, amqpSettings, transportListenerProvider.Object, webSockerListenerRegistry.Object, null, Mock.Of<IClientCredentialsFactory>()));
            Assert.Throws<ArgumentNullException>(() => new AmqpProtocolHead(transportSettings, amqpSettings, transportListenerProvider.Object, webSockerListenerRegistry.Object, Mock.Of<IAuthenticator>(), null));
            Assert.NotNull(new AmqpProtocolHead(transportSettings, amqpSettings, transportListenerProvider.Object, webSockerListenerRegistry.Object, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>()));
        }

        [Fact]
        [Unit]
        public async void TestStartAsyncThrowsIfCreateListenerThrows()
        {
            AmqpSettings amqpSettings = AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>(), Mock.Of<ILinkHandlerProvider>(), Mock.Of<IConnectionProvider>(), new NullCredentialsCache());

            var amqpTransportSettings = new Mock<TransportSettings>();
            amqpTransportSettings.Setup(ts => ts.CreateListener()).Throws(new ApplicationException("No donuts for you"));

            var transportSettings = new Mock<ITransportSettings>();
            transportSettings.SetupGet(sp => sp.Settings).Returns(amqpTransportSettings.Object);

            var protocolHead = new AmqpProtocolHead(transportSettings.Object, amqpSettings, Mock.Of<ITransportListenerProvider>(), Mock.Of<IWebSocketListenerRegistry>(), Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>());
            await Assert.ThrowsAsync<ApplicationException>(() => protocolHead.StartAsync());
        }

        [Fact]
        [Unit]
        public async void TestStartAsyncThrowsIfRootCreateTransportListenerThrows()
        {
            AmqpSettings amqpSettings = AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>(), Mock.Of<ILinkHandlerProvider>(), Mock.Of<IConnectionProvider>(), new NullCredentialsCache());

            var tcpTransportListener = new Mock<TransportListener>("TCP");
            var amqpTransportSettings = new Mock<TransportSettings>();
            amqpTransportSettings.Setup(ts => ts.CreateListener()).Returns(tcpTransportListener.Object);
            var transportSettings = new Mock<ITransportSettings>();
            transportSettings.SetupGet(sp => sp.Settings).Returns(amqpTransportSettings.Object);

            var transportListenerProvider = new Mock<ITransportListenerProvider>();
            transportListenerProvider.Setup(
                tlp => tlp.Create(
                    It.Is<IEnumerable<TransportListener>>(listeners => listeners.Contains(tcpTransportListener.Object)),
                    amqpSettings)).Throws(new ApplicationException("No donuts for you"));

            var protocolHead = new AmqpProtocolHead(transportSettings.Object, amqpSettings, transportListenerProvider.Object, Mock.Of<IWebSocketListenerRegistry>(), Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>());
            await Assert.ThrowsAsync<ApplicationException>(() => protocolHead.StartAsync());
        }

        [Theory]
        [MemberData(nameof(GetThrowingListeners))]
        [Unit]
        public async void TestStartAsyncThrowsIfOpenAsyncOrListenThrows(TransportListener amqpTransportListener)
        {
            AmqpSettings amqpSettings = AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>(), Mock.Of<ILinkHandlerProvider>(), Mock.Of<IConnectionProvider>(), new NullCredentialsCache());

            var tcpTransportListener = new Mock<TransportListener>("TCP");
            var amqpTransportSettings = new Mock<TransportSettings>();
            amqpTransportSettings.Setup(ts => ts.CreateListener()).Returns(tcpTransportListener.Object);
            var transportSettings = new Mock<ITransportSettings>();
            transportSettings.SetupGet(sp => sp.Settings).Returns(amqpTransportSettings.Object);

            var transportListenerProvider = new Mock<ITransportListenerProvider>();
            transportListenerProvider.Setup(
                tlp => tlp.Create(
                    It.Is<IEnumerable<TransportListener>>(listeners => listeners.Contains(tcpTransportListener.Object)),
                    amqpSettings)).Returns(amqpTransportListener);

            var protocolHead = new AmqpProtocolHead(transportSettings.Object, amqpSettings, transportListenerProvider.Object, Mock.Of<IWebSocketListenerRegistry>(), Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>());
            await Assert.ThrowsAsync<ApplicationException>(() => protocolHead.StartAsync());
        }

        [Fact]
        [Unit]
        public async void TestStartAsyncThrowsIfTransportListenerCallbackArgsHasException()
        {
            AmqpSettings amqpSettings = AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>(), Mock.Of<ILinkHandlerProvider>(), Mock.Of<IConnectionProvider>(), new NullCredentialsCache());

            var tcpTransportListener = new Mock<TransportListener>("TCP");
            var amqpTransportSettings = new Mock<TransportSettings>();
            amqpTransportSettings.Setup(ts => ts.CreateListener()).Returns(tcpTransportListener.Object);
            var transportSettings = new Mock<ITransportSettings>();
            transportSettings.SetupGet(sp => sp.Settings).Returns(amqpTransportSettings.Object);

            var amqpTransportListener = new TestHelperTransportListener(
                "AMQP",
                new TransportAsyncCallbackArgs()
                {
                    Exception = new ApplicationException("No donuts"),
                    CompletedSynchronously = false
                });

            var transportListenerProvider = new Mock<ITransportListenerProvider>();
            transportListenerProvider.Setup(
                tlp => tlp.Create(
                    It.Is<IEnumerable<TransportListener>>(listeners => listeners.Contains(tcpTransportListener.Object)),
                    amqpSettings)).Returns(amqpTransportListener);

            var protocolHead = new AmqpProtocolHead(transportSettings.Object, amqpSettings, transportListenerProvider.Object, Mock.Of<IWebSocketListenerRegistry>(), Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>());
            await Assert.ThrowsAsync<ApplicationException>(() => protocolHead.StartAsync());
        }

        [Fact]
        [Unit]
        public async void TestStartAsyncDoesNotThrowIfCreateConnectionThrows()
        {
            AmqpSettings amqpSettings = AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>(), Mock.Of<ILinkHandlerProvider>(), Mock.Of<IConnectionProvider>(), new NullCredentialsCache());
            var runtimeProvider = new Mock<IRuntimeProvider>();
            amqpSettings.RuntimeProvider = runtimeProvider.Object;

            runtimeProvider.Setup(rp => rp.CreateConnection(It.IsAny<TransportBase>(), It.IsAny<ProtocolHeader>(), false, It.IsAny<AmqpSettings>(), It.IsAny<AmqpConnectionSettings>()))
                .Throws(new ApplicationException("No donuts"));

            var tcpTransportListener = new Mock<TransportListener>("TCP");
            var amqpTransportSettings = new Mock<TransportSettings>();
            amqpTransportSettings.Setup(ts => ts.CreateListener()).Returns(tcpTransportListener.Object);
            var transportSettings = new Mock<ITransportSettings>();
            transportSettings.SetupGet(sp => sp.Settings).Returns(amqpTransportSettings.Object);

            var amqpTransportListener = new TestHelperTransportListener(
                "AMQP",
                new TransportAsyncCallbackArgs()
                {
                    CompletedSynchronously = false
                });

            var transportListenerProvider = new Mock<ITransportListenerProvider>();
            transportListenerProvider.Setup(
                tlp => tlp.Create(
                    It.Is<IEnumerable<TransportListener>>(listeners => listeners.Contains(tcpTransportListener.Object)),
                    amqpSettings)).Returns(amqpTransportListener);

            var protocolHead = new AmqpProtocolHead(transportSettings.Object, amqpSettings, transportListenerProvider.Object, Mock.Of<IWebSocketListenerRegistry>(), Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>());
            await protocolHead.StartAsync();
        }

        [Fact]
        [Unit]
        public async void TestStartAsyncDoesNotThrowIfConnectionOpenAsyncThrows()
        {
            AmqpSettings amqpSettings = AmqpSettingsProvider.GetDefaultAmqpSettings(IotHubHostName, Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>(), Mock.Of<ILinkHandlerProvider>(), Mock.Of<IConnectionProvider>(), new NullCredentialsCache());
            var runtimeProvider = new Mock<IRuntimeProvider>();
            amqpSettings.RuntimeProvider = runtimeProvider.Object;

            var tcpTransport = new Mock<TransportBase>("TCP");

            TestHelperAmqpConnection amqpConnection = null;
            runtimeProvider.Setup(rp => rp.CreateConnection(tcpTransport.Object, It.IsAny<ProtocolHeader>(), false, It.IsAny<AmqpSettings>(), It.IsAny<AmqpConnectionSettings>()))
                .Callback(
                    (
                        TransportBase transport,
                        ProtocolHeader protocolHeader,
                        bool isInitiator,
                        AmqpSettings settings,
                        AmqpConnectionSettings connectionSettings) =>
                    {
                        amqpConnection = new TestHelperAmqpConnection(transport, protocolHeader, isInitiator, settings, connectionSettings);
                        amqpConnection.OnOpenInternal = () => throw new OperationCanceledException("No donuts for you");
                    })
                .Returns(() => amqpConnection);

            var tcpTransportListener = new Mock<TransportListener>("TCP");
            var amqpTransportSettings = new Mock<TransportSettings>();
            amqpTransportSettings.Setup(ts => ts.CreateListener()).Returns(tcpTransportListener.Object);
            var transportSettings = new Mock<ITransportSettings>();
            transportSettings.SetupGet(sp => sp.Settings).Returns(amqpTransportSettings.Object);

            var amqpTransportListener = new TestHelperTransportListener(
                "AMQP",
                new TransportAsyncCallbackArgs()
                {
                    CompletedSynchronously = false,
                    Transport = tcpTransport.Object
                });

            var transportListenerProvider = new Mock<ITransportListenerProvider>();
            transportListenerProvider.Setup(
                tlp => tlp.Create(
                    It.Is<IEnumerable<TransportListener>>(listeners => listeners.Contains(tcpTransportListener.Object)),
                    amqpSettings)).Returns(amqpTransportListener);

            var protocolHead = new AmqpProtocolHead(transportSettings.Object, amqpSettings, transportListenerProvider.Object, Mock.Of<IWebSocketListenerRegistry>(), Mock.Of<IAuthenticator>(), Mock.Of<IClientCredentialsFactory>());
            await protocolHead.StartAsync();

            // check if close on the connection was called
            Assert.NotNull(amqpConnection);
            Assert.True(amqpConnection.WasClosed);
        }

        public static IEnumerable<object[]> GetThrowingListeners() => new[]
        {
            // Causes OpenAsync to throw
            new[] { new ThrowingTransportListener("AMQP", new ApplicationException("No donuts for you"), null) },

            // Causes Listen to throw
            new[] { new ThrowingTransportListener("AMQP", null, new ApplicationException("No donuts for you")) }
        };

        class TestHelperAmqpConnection : AmqpConnection
        {
            public TestHelperAmqpConnection(
                TransportBase transport,
                ProtocolHeader protocolHeader,
                bool
                    isInitiator,
                AmqpSettings amqpSettings,
                AmqpConnectionSettings connectionSettings)
                : base(transport, protocolHeader, isInitiator, amqpSettings, connectionSettings)
            {
            }

            public Action OnOpenInternal { get; set; }

            public bool WasClosed { get; set; }

            protected override bool OpenInternal()
            {
                this.OnOpenInternal?.Invoke();
                return base.OpenInternal();
            }

            protected override bool CloseInternal()
            {
                this.WasClosed = true;
                return base.CloseInternal();
            }
        }

        class TestHelperTransportListener : TransportListener
        {
            readonly TransportAsyncCallbackArgs callbackArgs;

            public TestHelperTransportListener(string type, TransportAsyncCallbackArgs callbackArgs)
                : base(type)
            {
                this.callbackArgs = callbackArgs;
            }

            protected override void OnListen()
            {
                this.OnTransportAccepted(this.callbackArgs);
            }
        }

        class ThrowingTransportListener : TransportListener
        {
            readonly Exception openException;
            readonly Exception listenException;

            public ThrowingTransportListener(string type, Exception openException, Exception listenException)
                : base(type)
            {
                this.openException = openException;
                this.listenException = listenException;
            }

            protected override bool OpenInternal()
            {
                if (this.openException != null)
                {
                    throw this.openException;
                }

                return true;
            }

            protected override void OnListen()
            {
                if (this.listenException != null)
                {
                    throw this.listenException;
                }
            }
        }
    }
}
