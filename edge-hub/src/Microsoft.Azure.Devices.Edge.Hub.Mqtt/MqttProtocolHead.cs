// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
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

        const int MqttsPort = 8883;
        const int DefaultListenBacklogSize = 200; // connections allowed pending accept
        const int DefaultParentEventLoopCount = 1;
        const int DefaultMaxInboundMessageSize = 256 * 1024;
        const int DefaultThreadCount = 200;
        IChannel serverChannel;
        IEventLoopGroup eventLoopGroup;

        public MqttProtocolHead(ISettingsProvider settingsProvider,
            X509Certificate tlsCertificate,
            IMqttConnectionProvider mqttConnectionProvider,
            IDeviceIdentityProvider identityProvider,
            ISessionStatePersistenceProvider sessionProvider)
        {
            this.settingsProvider = Preconditions.CheckNotNull(settingsProvider, nameof(settingsProvider));
            this.tlsCertificate = Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate));
            this.mqttConnectionProvider = Preconditions.CheckNotNull(mqttConnectionProvider, nameof(mqttConnectionProvider));
            this.identityProvider = Preconditions.CheckNotNull(identityProvider, nameof(identityProvider));            
            this.sessionProvider = Preconditions.CheckNotNull(sessionProvider, nameof(sessionProvider));
        }

        public async Task StartAsync()
        {
            try
            {
                this.logger.LogInformation("Starting");

                ServerBootstrap bootstrap = this.SetupServerBoostrap();

                this.logger.LogInformation("Initializing TLS endpoint on port {0}.", MqttsPort);

                this.serverChannel = await bootstrap.BindAsync(IPAddress.Any, MqttsPort);

                this.logger.LogInformation("Started");
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

                this.logger.LogInformation("Stopped");
            }
            catch (Exception ex)
            {
                this.logger.LogError("Failed to stop cleanly", ex);
            }
        }

        public void Dispose()
        {
        }

        ServerBootstrap SetupServerBoostrap()
        {
            int maxInboundMessageSize = this.settingsProvider.GetIntegerSetting("MaxInboundMessageSize", DefaultMaxInboundMessageSize);
            int threadCount = this.settingsProvider.GetIntegerSetting("ThreadCount", DefaultThreadCount);
            int listenBacklogSize = this.settingsProvider.GetIntegerSetting("ListenBacklogSize", DefaultListenBacklogSize);
            int parentEventLoopCount = this.settingsProvider.GetIntegerSetting("EventLoopCount", DefaultParentEventLoopCount);

            MessagingBridgeFactoryFunc bridgeFactory = this.mqttConnectionProvider.Connect;

            var boostrap = new ServerBootstrap();
            // multithreaded event loop that handles the incoming connection
            IEventLoopGroup parentEventLoopGroup = new MultithreadEventLoopGroup(parentEventLoopCount);
            // multithreaded event loop (worker) that handles the traffic of the accepted connections
            this.eventLoopGroup = new MultithreadEventLoopGroup(threadCount);

            boostrap.Group(parentEventLoopGroup, this.eventLoopGroup)
                .Option(ChannelOption.SoBacklog, listenBacklogSize)
                .ChildOption(ChannelOption.Allocator, PooledByteBufferAllocator.Default)
                .ChildOption(ChannelOption.AutoRead, false)
                // channel that accepts incoming connections
                .Channel<TcpServerSocketChannel>()
                // Channel initializer, it is handler that is purposed to help configure a new channel
                .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    // configure the channel pipeline of the new Channel by adding handlers
                    channel.Pipeline.AddLast(TlsHandler.Server(this.tlsCertificate));

                    channel.Pipeline.AddLast(
                        MqttEncoder.Instance,
                        new MqttDecoder(true, maxInboundMessageSize),
                        new MqttAdapter(
                            new Settings(this.settingsProvider),
                            this.sessionProvider,
                            this.identityProvider,
                            null,
                            bridgeFactory));
                }));

            return boostrap;
        }
    }
}
