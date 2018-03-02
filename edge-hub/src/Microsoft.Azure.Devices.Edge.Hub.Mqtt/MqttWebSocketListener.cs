// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Transport.Channels;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    class MqttWebSocketListener : IWebSocketListener
    {
        const string MqttWebSocketProtocol = "mqtt";

        readonly Settings settings;
        readonly MessagingBridgeFactoryFunc messagingBridgeFactoryFunc;
        readonly IDeviceIdentityProvider identityProvider;
        readonly Func<ISessionStatePersistenceProvider> sessionProviderFactory;
        readonly IEventLoopGroup workerGroup;
        readonly IByteBufferAllocator byteBufferAllocator;
        readonly bool autoRead;
        readonly int mqttDecoderMaxMessageSize;

        public MqttWebSocketListener(
            Settings settings,
            MessagingBridgeFactoryFunc messagingBridgeFactoryFunc,
            IDeviceIdentityProvider identityProvider,
            Func<ISessionStatePersistenceProvider> sessionProviderFactory,
            IEventLoopGroup workerGroup,
            IByteBufferAllocator byteBufferAllocator,
            bool autoRead,
            int mqttDecoderMaxMessageSize
            )
        {
            this.settings = Preconditions.CheckNotNull(settings, nameof(settings));
            this.messagingBridgeFactoryFunc = Preconditions.CheckNotNull(messagingBridgeFactoryFunc, nameof(messagingBridgeFactoryFunc));
            this.identityProvider = Preconditions.CheckNotNull(identityProvider, nameof(identityProvider));
            this.sessionProviderFactory = Preconditions.CheckNotNull(sessionProviderFactory, nameof(sessionProviderFactory));
            this.workerGroup = Preconditions.CheckNotNull(workerGroup, nameof(workerGroup));
            this.byteBufferAllocator = Preconditions.CheckNotNull(byteBufferAllocator, nameof(byteBufferAllocator));
            this.autoRead = autoRead;
            this.mqttDecoderMaxMessageSize = mqttDecoderMaxMessageSize;
        }

        public string SubProtocol => MqttWebSocketProtocol;

        public async Task ProcessWebSocketRequestAsync(HttpContext context, string correlationId)
        {
            try
            {
                WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync(this.SubProtocol);

                var serverChannel = new ServerWebSocketChannel(
                    webSocket,
                    new IPEndPoint(context.Connection.RemoteIpAddress, context.Connection.RemotePort)
                );

                serverChannel
                    .Option(ChannelOption.Allocator, this.byteBufferAllocator)
                    .Option(ChannelOption.AutoRead, this.autoRead)
                    .Option(ChannelOption.RcvbufAllocator, new AdaptiveRecvByteBufAllocator())
                    .Option(ChannelOption.MessageSizeEstimator, DefaultMessageSizeEstimator.Default)
                    .Pipeline.AddLast(
                        MqttEncoder.Instance,
                        new MqttDecoder(true, this.mqttDecoderMaxMessageSize),
                        new MqttAdapter(
                            this.settings,
                            this.sessionProviderFactory(),
                            this.identityProvider,
                            null,
                            this.messagingBridgeFactoryFunc));

                await this.workerGroup.GetNext().RegisterAsync(serverChannel);

                Events.Established(context.Request.Host.Host, correlationId);

                await serverChannel.WebSocketClosed.Task;  // This will wait until the websocket is closed 
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.Exception(context.Request.Host.Host, correlationId, ex);
                throw;
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<MqttWebSocketListener>();
            const int IdStart = MqttEventIds.MqttWebSocketListener;

            enum EventIds
            {
                Established = IdStart,
                Exception
            }

            public static void Established(string hostName, string correlationId)
            {
                Log.LogInformation((int)Events.EventIds.Established, Invariant($"HostName: {hostName} CorrelationId {correlationId}"));
            }

            public static void Exception(string hostName, string correlationId, Exception ex)
            {
                Log.LogWarning((int)Events.EventIds.Exception, ex, Invariant($"HostName: {hostName} CorrelationId {correlationId}"));
            }
        }
    }
}
