// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    public class AmqpProtocolHead : IProtocolHead
    {
        readonly AmqpConnectionSettings connectionSettings;
        readonly ITransportSettings transportSettings;
        readonly AmqpSettings amqpSettings;
        readonly ITransportListenerProvider transportListenerProvider;
        readonly IWebSocketListenerRegistry webSocketListenerRegistry;
        readonly ConcurrentDictionary<uint, AmqpConnection> incomingConnectionMap;
        readonly AsyncLock syncLock;

        TransportListener amqpTransportListener;

        public AmqpProtocolHead(
            ITransportSettings transportSettings,
            AmqpSettings amqpSettings,
            ITransportListenerProvider transportListenerProvider,
            IWebSocketListenerRegistry webSocketListenerRegistry)
        {
            this.syncLock = new AsyncLock();
            this.transportSettings = Preconditions.CheckNotNull(transportSettings, nameof(transportSettings));
            this.amqpSettings = Preconditions.CheckNotNull(amqpSettings, nameof(amqpSettings));
            this.transportListenerProvider = Preconditions.CheckNotNull(transportListenerProvider);
            this.webSocketListenerRegistry = Preconditions.CheckNotNull(webSocketListenerRegistry);

            this.connectionSettings = new AmqpConnectionSettings
            {
                ContainerId = "DeviceGateway_" + Guid.NewGuid().ToString("N"),
                HostName = transportSettings.HostName,
                // 'IdleTimeOut' on connection settings will be used to close connection if server hasn't 
                // received any packet for 'IdleTimeout'
                // Open frame send to client will have the IdleTimeout set and the client will do heart beat
                // every 'IdleTimeout * 7 / 8'
                // If server doesn't receive any packet till 'ServiceMaxConnectionIdleTimeout' the connection will be closed
                IdleTimeOut = Constants.DefaultAmqpConnectionIdleTimeoutInMilliSeconds
            };

            this.incomingConnectionMap = new ConcurrentDictionary<uint, AmqpConnection>();
        }

        public string Name => "AMQP";

        public async Task StartAsync()
        {
            Events.Starting();

            var amqpWebSocketListener = new AmqpWebSocketListener();
            // This transport settings object sets up a listener for TLS over TCP and a listener for WebSockets.
            TransportListener[] listeners = { this.transportSettings.Settings.CreateListener(), amqpWebSocketListener };

            using (await this.syncLock.LockAsync())
            {
                this.amqpTransportListener = this.transportListenerProvider.Create(listeners, this.amqpSettings);
                await this.amqpTransportListener.OpenAsync(TimeSpan.FromMinutes(1));
                this.amqpTransportListener.Listen(this.OnAcceptTransport);
            }

            if (this.webSocketListenerRegistry.TryRegister(amqpWebSocketListener))
            {
                Events.RegisteredWebSocketListener();
            }
            else
            {
                Events.RegisterWebSocketListenerFailed();
            }

            // Preallocate buffers for AMQP transport
            ByteBuffer.InitBufferManagers();

            Events.Started();
        }

        public async Task CloseAsync(CancellationToken token)
        {
            if (this.amqpTransportListener != null)
            {
                using (await this.syncLock.LockAsync(token))
                {
                    if (this.amqpTransportListener != null)
                    {
                        this.amqpTransportListener.Close();
                        this.amqpTransportListener = null;
                    }
                }
            }

            // Close all existing connections
            this.SafeCloseExistingConnections();
        }

        void OnAcceptTransport(TransportListener transportListener, TransportAsyncCallbackArgs args)
        {
            if (args.Exception != null)
            {
                Events.AcceptTransportInputError(args.Exception);
                throw args.Exception;
            }

            AmqpConnection connection = null;

            try
            {
                connection = this.amqpSettings.RuntimeProvider.CreateConnection(
                    args.Transport,
                    (ProtocolHeader)args.UserToken,
                    false,
                    this.amqpSettings.Clone(),
                    this.connectionSettings.Clone()
                );

                // Open the connection async but don't block waiting on it.
                this.OpenAmqpConnectionAsync(connection, AmqpConstants.DefaultTimeout)
                    .ContinueWith(task =>
                    {
                        if (task.Exception != null)
                        {
                            Events.OpenConnectionError(task.Exception);
                        }
                    }, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex) when (ex.IsFatal() == false)
            {
                Events.AcceptTransportError(ex);
                connection?.SafeClose(ex);
            }
        }

        async Task OpenAmqpConnectionAsync(AmqpConnection amqpConnection, TimeSpan defaultTimeout)
        {
            if (await SafeOpenConnectionAsync())
            {
                try
                {
                    if (this.incomingConnectionMap.TryAdd(amqpConnection.Identifier.Value, amqpConnection))
                    {
                        amqpConnection.SafeAddClosed((s, e) => this.incomingConnectionMap.TryRemove(amqpConnection.Identifier.Value, out AmqpConnection _));
                    }
                    else
                    {
                        Events.ConnectionContextAddFailed(amqpConnection.Identifier.Value);
                    }
                }
                catch (Exception e) when (!e.IsFatal())
                {
                    Events.OpenConnectionError(e);
                    amqpConnection.SafeClose(e);
                }
            }

            async Task<bool> SafeOpenConnectionAsync()
            {
                try
                {
                    await amqpConnection.OpenAsync(defaultTimeout);
                    return true;
                }
                catch (Exception e) when (!e.IsFatal())
                {
                    Events.AmqpConnectionOpenAsyncFailed(e);

                    switch (e)
                    {
                        case OperationCanceledException _:
                            amqpConnection.SafeClose(new EdgeHubConnectionException("Operation Canceled Exception"));
                            break;

                        case TimeoutException _:
                            amqpConnection.SafeClose(new EdgeHubConnectionException("Timeout Exception"));
                            break;

                        case IOException _:
                            amqpConnection.SafeClose(new EdgeHubConnectionException("IO Exception"));
                            break;

                        case SocketException _:
                            amqpConnection.SafeClose(new EdgeHubConnectionException("Socket Exception"));
                            break;

                        case AmqpException ae when ae.Error.Condition.Equals(AmqpErrorCode.ConnectionForced):
                            amqpConnection.SafeClose(new EdgeHubConnectionException("AMQP connection forced exception"));
                            break;

                        default:
                            amqpConnection.SafeClose(e);
                            break;
                    }

                    return false;
                }
            }
        }

        void SafeCloseExistingConnections()
        {
            var connectionSnapShot = new List<AmqpConnection>(this.incomingConnectionMap.Values);
            connectionSnapShot.ForEach(conn => conn.SafeClose(new AmqpException(AmqpErrorCode.DetachForced, "Server busy, please retry operation")));
        }

        public void Dispose()
        {
            this.CloseAsync(CancellationToken.None).Wait();
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<AmqpProtocolHead>();
            const int IdStart = AmqpEventIds.AmqpProtocolHead;

            enum EventIds
            {
                AcceptTransportInputError = IdStart,
                AcceptTransportError = IdStart + 1,
                AmqpConnectionOpenAsyncFailed = IdStart + 2,
                ConnectionContextAddFailed = IdStart + 3,
                Starting = IdStart + 4,
                Started = IdStart + 5,
                WebSocketsRegistered,
                WebSocketsRegisterFail
            }

            internal static void AcceptTransportInputError(Exception ex) => Log.LogError((int)EventIds.AcceptTransportInputError, ex, $"Received a new transport connection with an error.");

            internal static void AcceptTransportError(Exception ex) => Log.LogError((int)EventIds.AcceptTransportError, ex, $"An error occurred while accepting a connection.");

            internal static void OpenConnectionError(Exception ex) => Log.LogError((int)EventIds.AcceptTransportInputError, ex, $"An error occurred while opening a connection.");

            internal static void AmqpConnectionOpenAsyncFailed(Exception ex) => Log.LogError((int)EventIds.AmqpConnectionOpenAsyncFailed, ex, $"An error occurred while opening AMQP connection.");

            internal static void ConnectionContextAddFailed(uint id) => Log.LogError((int)EventIds.ConnectionContextAddFailed, $"Failed to add to map for sequence # {id}");

            internal static void Starting() => Log.LogInformation((int)EventIds.Starting, $"Starting AMQP head");

            internal static void Started() => Log.LogInformation((int)EventIds.Started, $"Started AMQP head");

            internal static void RegisteredWebSocketListener() => Log.LogDebug((int)EventIds.WebSocketsRegistered, "WebSockets listener registered.");

            internal static void RegisterWebSocketListenerFailed() => Log.LogDebug((int)EventIds.WebSocketsRegisterFail, "WebSockets listener failed to register.");
        }
    }
}
