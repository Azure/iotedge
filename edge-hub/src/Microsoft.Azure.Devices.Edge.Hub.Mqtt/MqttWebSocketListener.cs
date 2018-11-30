// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.WebSockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Extensions.Logging;
    using static System.FormattableString;

    class MqttWebSocketListener : IWebSocketListener
    { 
        readonly Settings settings;
        readonly MessagingBridgeFactoryFunc messagingBridgeFactoryFunc;
        readonly IAuthenticator authenticator;
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly Func<ISessionStatePersistenceProvider> sessionProviderFactory;
        readonly IEventLoopGroup workerGroup;
        readonly IByteBufferAllocator byteBufferAllocator;
        readonly bool autoRead;
        readonly int mqttDecoderMaxMessageSize;

        public MqttWebSocketListener(
            Settings settings,
            MessagingBridgeFactoryFunc messagingBridgeFactoryFunc,
            IAuthenticator authenticator,
            IClientCredentialsFactory clientCredentialsFactory,
            Func<ISessionStatePersistenceProvider> sessionProviderFactory,
            IEventLoopGroup workerGroup,
            IByteBufferAllocator byteBufferAllocator,
            bool autoRead,
            int mqttDecoderMaxMessageSize
            )
        {
            this.settings = Preconditions.CheckNotNull(settings, nameof(settings));
            this.messagingBridgeFactoryFunc = Preconditions.CheckNotNull(messagingBridgeFactoryFunc, nameof(messagingBridgeFactoryFunc));
            this.authenticator = Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            this.clientCredentialsFactory = Preconditions.CheckNotNull(clientCredentialsFactory, nameof(clientCredentialsFactory));
            this.sessionProviderFactory = Preconditions.CheckNotNull(sessionProviderFactory, nameof(sessionProviderFactory));
            this.workerGroup = Preconditions.CheckNotNull(workerGroup, nameof(workerGroup));
            this.byteBufferAllocator = Preconditions.CheckNotNull(byteBufferAllocator, nameof(byteBufferAllocator));
            this.autoRead = autoRead;
            this.mqttDecoderMaxMessageSize = mqttDecoderMaxMessageSize;
        }

        public string SubProtocol => Constants.WebSocketSubProtocol;

        public async Task ProcessWebSocketRequestAsync(WebSocket webSocket, Option<EndPoint> localEndPoint, EndPoint remoteEndPoint, string correlationId)
        {
            try
            {
                DeviceIdentityProvider identityProvider = new DeviceIdentityProvider(this.authenticator, this.clientCredentialsFactory, true);
                var serverChannel = new ServerWebSocketChannel(
                    webSocket,
                    remoteEndPoint
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
                            identityProvider,
                            null,
                            this.messagingBridgeFactoryFunc));

                await this.workerGroup.GetNext().RegisterAsync(serverChannel);

                Events.Established(correlationId);

                await serverChannel.WebSocketClosed.Task;  // This will wait until the websocket is closed 
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.Exception(correlationId, ex);
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

            public static void Established(string correlationId)
            {
                Log.LogInformation((int)Events.EventIds.Established, Invariant($"CorrelationId {correlationId}"));
            }

            public static void Exception(string correlationId, Exception ex)
            {
                Log.LogWarning((int)Events.EventIds.Exception, ex, Invariant($"CorrelationId {correlationId}"));
            }
        }
    }
}
