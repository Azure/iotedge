// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Security;
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
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Logging;

    public class MqttProtocolHead : IProtocolHead
    {
        readonly ILogger logger = Logger.Factory.CreateLogger<MqttProtocolHead>();
        readonly ISettingsProvider settingsProvider;
        readonly X509Certificate tlsCertificate;
        readonly ISessionStatePersistenceProvider sessionProvider;
        readonly IMqttConnectionProvider mqttConnectionProvider;
        readonly IDeviceIdentityProvider identityProvider;
        readonly IWebSocketListenerRegistry webSocketListenerRegistry;
        readonly IByteBufferAllocator byteBufferAllocator = PooledByteBufferAllocator.Default;
        readonly Option<IList<X509Certificate2>> caCertChain;
        readonly bool clientCertAuthAllowed;

        const int MqttsPort = 8883;
        const int DefaultListenBacklogSize = 200; // connections allowed pending accept
        const int DefaultParentEventLoopCount = 1;
        const int DefaultMaxInboundMessageSize = 256 * 1024;
        const int DefaultThreadCount = 200;
        const bool AutoRead = false;
        IChannel serverChannel;
        IEventLoopGroup eventLoopGroup;

        public MqttProtocolHead(ISettingsProvider settingsProvider,
            X509Certificate tlsCertificate,
            IMqttConnectionProvider mqttConnectionProvider,
            IDeviceIdentityProvider identityProvider,
            ISessionStatePersistenceProvider sessionProvider,
            IWebSocketListenerRegistry webSocketListenerRegistry,
            bool clientCertAuthAllowed,
            string caChainPath)
        {
            this.settingsProvider = Preconditions.CheckNotNull(settingsProvider, nameof(settingsProvider));
            this.tlsCertificate = Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate));
            this.mqttConnectionProvider = Preconditions.CheckNotNull(mqttConnectionProvider, nameof(mqttConnectionProvider));
            this.identityProvider = Preconditions.CheckNotNull(identityProvider, nameof(identityProvider));
            this.sessionProvider = Preconditions.CheckNotNull(sessionProvider, nameof(sessionProvider));
            this.webSocketListenerRegistry = Preconditions.CheckNotNull(webSocketListenerRegistry, nameof(webSocketListenerRegistry));
            this.clientCertAuthAllowed = clientCertAuthAllowed;
            this.caCertChain = clientCertAuthAllowed
                ? this.GetCaChainCerts(Preconditions.CheckNonWhiteSpace(caChainPath, nameof(this.caCertChain)))
                : Option.None<IList<X509Certificate2>>();
        }

        public string Name => "MQTT";

        public async Task StartAsync()
        {
            try
            {
                this.logger.LogInformation("Starting MQTT head");

                ServerBootstrap bootstrap = this.SetupServerBoostrap();

                this.logger.LogInformation("Initializing TLS endpoint on port {0} for MQTT head.", MqttsPort);

                this.serverChannel = await bootstrap.BindAsync(IPAddress.IPv6Any, MqttsPort);

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
                this.logger.LogInformation("Stopping");

                await (this.serverChannel?.CloseAsync() ?? TaskEx.Done);
                await (this.eventLoopGroup?.ShutdownGracefullyAsync() ?? TaskEx.Done);
                // TODO: gracefully shutdown the MultithreadEventLoopGroup in MqttWebSocketListener?
                // TODO: this.webSocketListenerRegistry.TryUnregister("mqtts")?

                this.logger.LogInformation("Stopped");
            }
            catch (Exception ex)
            {
                this.logger.LogError("Failed to stop cleanly", ex);
            }
        }

        public void Dispose()
        {
            this.mqttConnectionProvider.Dispose();
            this.CloseAsync(CancellationToken.None).Wait();
        }

        ServerBootstrap SetupServerBoostrap()
        {
            int maxInboundMessageSize = this.settingsProvider.GetIntegerSetting("MaxInboundMessageSize", DefaultMaxInboundMessageSize);
            int threadCount = this.settingsProvider.GetIntegerSetting("ThreadCount", DefaultThreadCount);
            int listenBacklogSize = this.settingsProvider.GetIntegerSetting("ListenBacklogSize", DefaultListenBacklogSize);
            int parentEventLoopCount = this.settingsProvider.GetIntegerSetting("EventLoopCount", DefaultParentEventLoopCount);
            var settings = new Settings(this.settingsProvider);

            MessagingBridgeFactoryFunc bridgeFactory = this.mqttConnectionProvider.Connect;

            var boostrap = new ServerBootstrap();
            // multithreaded event loop that handles the incoming connection
            IEventLoopGroup parentEventLoopGroup = new MultithreadEventLoopGroup(parentEventLoopCount);
            // multithreaded event loop (worker) that handles the traffic of the accepted connections
            this.eventLoopGroup = new MultithreadEventLoopGroup(threadCount);

            boostrap.Group(parentEventLoopGroup, this.eventLoopGroup)
                .Option(ChannelOption.SoBacklog, listenBacklogSize)
                // Allow listening socket to force bind to port if previous socket is still in TIME_WAIT
                // Fixes "address is already in use" errors
                .Option(ChannelOption.SoReuseaddr, true)
                .ChildOption(ChannelOption.Allocator, this.byteBufferAllocator)
                .ChildOption(ChannelOption.AutoRead, AutoRead)
                // channel that accepts incoming connections
                .Channel<TcpServerSocketChannel>()
                // Channel initializer, it is handler that is purposed to help configure a new channel
                .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    // configure the channel pipeline of the new Channel by adding handlers
                    TlsSettings serverSettings = new ServerTlsSettings(
                            certificate: this.tlsCertificate,
                            negotiateClientCertificate: this.clientCertAuthAllowed
                        );

                    channel.Pipeline.AddLast(new TlsHandler(stream =>
                        new SslStream(stream,
                                      true,
                                      (sender, remoteCertificate, remoteChain, sslPolicyErrors) =>
                                      this.clientCertAuthAllowed ?
                                          CertificateHelper.ValidateClientCert(remoteCertificate, remoteChain, this.caCertChain, this.logger) : true),
                                      serverSettings));

                    channel.Pipeline.AddLast(
                        MqttEncoder.Instance,
                        new MqttDecoder(true, maxInboundMessageSize),
                        new MqttAdapter(
                            settings,
                            this.sessionProvider,
                            this.identityProvider,
                            null,
                            bridgeFactory));
                }));

            var mqttWebSocketListener = new MqttWebSocketListener(
                settings,
                bridgeFactory,
                this.identityProvider,
                () => this.sessionProvider,
                new MultithreadEventLoopGroup(Environment.ProcessorCount),
                this.byteBufferAllocator,
                AutoRead,
                maxInboundMessageSize);

            this.webSocketListenerRegistry.TryRegister(mqttWebSocketListener);

            return boostrap;
        }

        Option<IList<X509Certificate2>> GetCaChainCerts(string caChainPath)
        {
            if (!string.IsNullOrWhiteSpace(caChainPath))
            {
                (Option<IList<X509Certificate2>> caChainCerts, Option<string> errors) = CertificateHelper.GetCertsAtPath(caChainPath);
                errors.ForEach(v => this.logger.LogWarning(v));
                return caChainCerts;
            }
            return Option.None<IList<X509Certificate2>>();
        }
    }
}
