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
    using DotNetty.Common.Concurrency;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Azure.Devices.ProtocolGateway.IotHubClient.Addressing;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Logging;

    public class MqttBootstrapper
    {
        readonly ILogger logger = new LoggerFactory().AddConsole().CreateLogger<MqttBootstrapper>();
        readonly TimeSpan defaultConnectionIdleTimeout = TimeSpan.FromSeconds(210); // IoT Hub default connection idle timeout
        readonly ISettingsProvider settingsProvider;
        readonly X509Certificate tlsCertificate;
        readonly ISessionStatePersistenceProvider sessionStateManager;
        readonly IConnectionManager connectionManager;
        readonly int DefaultThreadCount = 200;
        
        readonly TaskCompletionSource closeCompletionSource;
        readonly IMqttConnectionProvider mqttConnectionProvider;
   
        const int MqttsPort = 8883;
        const int DefaultListenBacklogSize = 200; // connections allowed pending accept
        const int DefaultParentEventLoopCount = 1;
        const int DefaultMaxInboundMessageSize = 256 * 1024;
        IChannel serverChannel;
        IEventLoopGroup eventLoopGroup;

        public Task CloseCompletion => this.closeCompletionSource.Task;

        public MqttBootstrapper(ISettingsProvider settingsProvider,
            X509Certificate tlsCertificate,
            IMqttConnectionProvider mqttConnectionProvider,
            IConnectionManager connectionManager)
        {
            this.settingsProvider = Preconditions.CheckNotNull(settingsProvider, nameof(settingsProvider));
            this.tlsCertificate = Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate));
            this.mqttConnectionProvider = Preconditions.CheckNotNull(mqttConnectionProvider);
            this.connectionManager = Preconditions.CheckNotNull(connectionManager);

            this.closeCompletionSource = new TaskCompletionSource();
            this.sessionStateManager = new TransientSessionStatePersistenceProvider();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                this.logger.LogInformation("Starting");

                ServerBootstrap bootstrap = this.SetupServerBoostrap();

                this.logger.LogInformation("Initializing TLS endpoint on port {0}.", MqttsPort);

                this.serverChannel = await bootstrap.BindAsync(IPAddress.Any, MqttsPort);

                cancellationToken.Register(this.CloseAsync);

                this.logger.LogInformation("Started");
            }
            catch (Exception e)
            {
                this.logger.LogError("Failed to start MQTT server {0}", e);
                this.CloseAsync();
            }
        }

        private ServerBootstrap SetupServerBoostrap()
        {
            int maxInboundMessageSize = this.settingsProvider.GetIntegerSetting("MaxInboundMessageSize", DefaultMaxInboundMessageSize);            
            int threadCount = this.settingsProvider.GetIntegerSetting("ThreadCount", this.DefaultThreadCount);
            int listenBacklogSize = this.settingsProvider.GetIntegerSetting("ListenBacklogSize", DefaultListenBacklogSize);
            int parentEventLoopCount = this.settingsProvider.GetIntegerSetting("EventLoopCount", DefaultParentEventLoopCount);
            string iotHubHostName = this.settingsProvider.GetSetting("IotHubHostName");
            string edgeDeviceId = this.settingsProvider.GetSetting("EdgeDeviceId");

            var authenticator = new Authenticator(this.connectionManager, edgeDeviceId);
            var identityFactory = new IdentityFactory(iotHubHostName);
            var authProvider = new SasTokenDeviceIdentityProvider(authenticator, identityFactory);

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
                            this.sessionStateManager,
                            authProvider,
                            null,
                            bridgeFactory));
                }));

            return boostrap;
        }

        async void CloseAsync()
        {
            try
            {
                this.logger.LogInformation("Stopping");

                if (this.serverChannel != null)
                {
                    await this.serverChannel.CloseAsync();
                }
                if (this.eventLoopGroup != null)
                {
                    await this.eventLoopGroup.ShutdownGracefullyAsync();
                }

                this.logger.LogInformation("Stopped");
            }
            catch (Exception ex)
            {
                this.logger.LogError("Failed to stop cleanly", ex);
            }
            finally
            {
                this.closeCompletionSource.TryComplete();
            }
        }
    }
}
