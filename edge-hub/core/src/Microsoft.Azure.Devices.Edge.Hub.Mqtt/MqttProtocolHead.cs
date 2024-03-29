// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Logging;

    public class MqttProtocolHead : IProtocolHead
    {
        const int MqttsPort = 8883;
        const int DefaultListenBacklogSize = 200; // connections allowed pending accept
        const int DefaultParentEventLoopCount = 1;
        const int DefaultMaxInboundMessageSize = 256 * 1024;
        const bool AutoRead = false;
        static readonly TimeSpan TimeoutInSecs = TimeSpan.FromSeconds(20);

        readonly int defaultThreadCount = Environment.ProcessorCount * 2;
        readonly ILogger logger = Logger.Factory.CreateLogger<MqttProtocolHead>();
        readonly ISettingsProvider settingsProvider;
        readonly X509Certificate tlsCertificate;
        readonly ISessionStatePersistenceProvider sessionProvider;
        readonly IMqttConnectionProvider mqttConnectionProvider;
        readonly IAuthenticator authenticator;
        readonly IUsernameParser usernameParser;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly IWebSocketListenerRegistry webSocketListenerRegistry;
        readonly IByteBufferAllocator byteBufferAllocator;
        readonly IMetadataStore metadataStore;
        readonly bool clientCertAuthAllowed;
        readonly SslProtocols sslProtocols;

        IChannel serverChannel;
        IEventLoopGroup eventLoopGroup;
        IEventLoopGroup wsEventLoopGroup;
        IEventLoopGroup parentEventLoopGroup;

        public MqttProtocolHead(
            ISettingsProvider settingsProvider,
            X509Certificate tlsCertificate,
            IMqttConnectionProvider mqttConnectionProvider,
            IAuthenticator authenticator,
            IUsernameParser usernameParser,
            IClientCredentialsFactory clientCredentialsFactory,
            ISessionStatePersistenceProvider sessionProvider,
            IWebSocketListenerRegistry webSocketListenerRegistry,
            IByteBufferAllocator byteBufferAllocator,
            IMetadataStore metadataStore,
            bool clientCertAuthAllowed,
            SslProtocols sslProtocols)
        {
            this.settingsProvider = Preconditions.CheckNotNull(settingsProvider, nameof(settingsProvider));
            this.tlsCertificate = Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate));
            this.mqttConnectionProvider = Preconditions.CheckNotNull(mqttConnectionProvider, nameof(mqttConnectionProvider));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.usernameParser = Preconditions.CheckNotNull(usernameParser, nameof(usernameParser));
            this.clientCredentialsFactory = Preconditions.CheckNotNull(clientCredentialsFactory, nameof(clientCredentialsFactory));
            this.sessionProvider = Preconditions.CheckNotNull(sessionProvider, nameof(sessionProvider));
            this.webSocketListenerRegistry = Preconditions.CheckNotNull(webSocketListenerRegistry, nameof(webSocketListenerRegistry));
            this.byteBufferAllocator = Preconditions.CheckNotNull(byteBufferAllocator);
            this.clientCertAuthAllowed = clientCertAuthAllowed;
            this.metadataStore = Preconditions.CheckNotNull(metadataStore, nameof(metadataStore));
            this.sslProtocols = sslProtocols;
        }

        public string Name => "MQTT";

        public async Task StartAsync()
        {
            try
            {
                this.logger.LogInformation("Starting MQTT head");

                ServerBootstrap bootstrap = this.SetupServerBootstrap();

                this.logger.LogInformation("Initializing TLS endpoint on port {0} for MQTT head.", MqttsPort);

                this.serverChannel = await bootstrap.BindAsync(!Socket.OSSupportsIPv6 ? IPAddress.Any : IPAddress.IPv6Any, MqttsPort);

                this.logger.LogInformation("Started MQTT head");
            }
            catch (Exception e)
            {
                this.logger.LogError("Failed to start MQTT server {0}", e);
                await this.CloseAsync(CancellationToken.None);
            }
        }

        public async Task CloseAsync(CancellationToken token)
        {
            try
            {
                this.logger.LogInformation("Stopping MQTT protocol head");

                await (this.serverChannel?.CloseAsync() ?? TaskEx.Done);
                await TaskEx.TimeoutAfter(this.eventLoopGroup?.ShutdownGracefullyAsync(), TimeoutInSecs);
                await TaskEx.TimeoutAfter(this.parentEventLoopGroup?.ShutdownGracefullyAsync(), TimeoutInSecs);
                await TaskEx.TimeoutAfter(this.wsEventLoopGroup?.ShutdownGracefullyAsync(), TimeoutInSecs);
                // TODO: gracefully shutdown the MultithreadEventLoopGroup in MqttWebSocketListener?
                // TODO: this.webSocketListenerRegistry.TryUnregister("mqtts")?
                this.logger.LogInformation("Stopped MQTT protocol head");
            }
            catch (Exception ex)
            {
                this.logger.LogError($"Failed to stop cleanly, error: {ex}");
            }
        }

        public void Dispose()
        {
            this.mqttConnectionProvider.Dispose();
        }

        ServerBootstrap SetupServerBootstrap()
        {
            int maxInboundMessageSize = this.settingsProvider.GetIntegerSetting("MaxInboundMessageSize", DefaultMaxInboundMessageSize);
            int threadCount = this.settingsProvider.GetIntegerSetting("ThreadCount", this.defaultThreadCount);
            int listenBacklogSize = this.settingsProvider.GetIntegerSetting("ListenBacklogSize", DefaultListenBacklogSize);
            int parentEventLoopCount = this.settingsProvider.GetIntegerSetting("EventLoopCount", DefaultParentEventLoopCount);
            var settings = new Settings(this.settingsProvider);

            MessagingBridgeFactoryFunc bridgeFactory = this.mqttConnectionProvider.Connect;

            var bootstrap = new ServerBootstrap();
            // multithreaded event loop that handles the incoming connection
            this.parentEventLoopGroup = new MultithreadEventLoopGroup(parentEventLoopCount);
            // multithreaded event loop (worker) that handles the traffic of the accepted connections
            this.eventLoopGroup = new MultithreadEventLoopGroup(threadCount);
            bootstrap.Group(this.parentEventLoopGroup, this.eventLoopGroup)
                .Option(ChannelOption.SoBacklog, listenBacklogSize)
                // Allow listening socket to force bind to port if previous socket is still in TIME_WAIT
                // Fixes "address is already in use" errors
                .Option(ChannelOption.SoReuseaddr, true)
                .ChildOption(ChannelOption.Allocator, this.byteBufferAllocator)
                .ChildOption(ChannelOption.AutoRead, AutoRead)
                // channel that accepts incoming connections
                .Channel<TcpServerSocketChannel>()
                // Channel initializer, it is handler that is purposed to help configure a new channel
                .ChildHandler(
                    new ActionChannelInitializer<ISocketChannel>(
                        channel =>
                        {
                            var identityProvider = new DeviceIdentityProvider(this.authenticator, this.usernameParser, this.clientCredentialsFactory, this.metadataStore, this.clientCertAuthAllowed);

                            // configure the channel pipeline of the new Channel by adding handlers
                            TlsSettings serverSettings = new ServerTlsSettings(
                                certificate: this.tlsCertificate,
                                negotiateClientCertificate: this.clientCertAuthAllowed,
                                checkCertificateRevocation: false,
                                enabledProtocols: this.sslProtocols);

                            channel.Pipeline.AddLast(
                                new TlsHandler(
                                    stream =>
                                        new SslStream(
                                            stream,
                                            true,
                                            (sender, remoteCertificate, remoteChain, sslPolicyErrors) => this.RemoteCertificateValidationCallback(identityProvider, remoteCertificate, remoteChain)),
                                    serverSettings));

                            channel.Pipeline.AddLast(
                                MqttEncoder.Instance,
                                new MqttDecoder(true, maxInboundMessageSize),
                                new MqttAdapter(
                                    settings,
                                    this.sessionProvider,
                                    identityProvider,
                                    null,
                                    bridgeFactory));
                        }));

            this.wsEventLoopGroup = new MultithreadEventLoopGroup(Environment.ProcessorCount);
            var mqttWebSocketListener = new MqttWebSocketListener(
                settings,
                bridgeFactory,
                this.authenticator,
                this.usernameParser,
                this.clientCredentialsFactory,
                () => this.sessionProvider,
                this.wsEventLoopGroup,
                this.byteBufferAllocator,
                AutoRead,
                maxInboundMessageSize,
                this.clientCertAuthAllowed,
                this.metadataStore);

            this.webSocketListenerRegistry.TryRegister(mqttWebSocketListener);

            return bootstrap;
        }

        bool RemoteCertificateValidationCallback(DeviceIdentityProvider identityProvider, X509Certificate certificate, X509Chain chain)
        {
            if (this.clientCertAuthAllowed && certificate != null)
            {
                IList<X509Certificate2> certChain = chain?.ChainElements?
                        .Cast<X509ChainElement>()
                        .Select(element => new X509Certificate2(element.Certificate))
                        .ToList()
                    ?? new List<X509Certificate2>();

                identityProvider.RegisterConnectionCertificate(new X509Certificate2(certificate), certChain);
            }

            return true;
        }
    }
}
