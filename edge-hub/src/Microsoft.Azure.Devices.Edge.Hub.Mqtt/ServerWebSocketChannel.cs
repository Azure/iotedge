// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    using static System.FormattableString;

    public sealed class ServerWebSocketChannel : AbstractChannel, IDisposable
    {
        readonly WebSocket webSocket;
        readonly CancellationTokenSource writeCancellationTokenSource;
        readonly TaskCompletionSource<int> webSocketClosed;
        readonly string correlationId;

        volatile bool active;

        public ServerWebSocketChannel(WebSocket webSocket, EndPoint remoteEndPoint)
            : base(null)
        {
            this.webSocket = Preconditions.CheckNotNull(webSocket, nameof(webSocket));
            this.RemoteAddressInternal = Preconditions.CheckNotNull(remoteEndPoint, nameof(remoteEndPoint));
            this.active = true;
            this.Metadata = new ChannelMetadata(false, 16);
            this.Configuration = new ServerWebSocketChannelConfig();
            this.writeCancellationTokenSource = new CancellationTokenSource();
            this.webSocketClosed = new TaskCompletionSource<int>();
            this.correlationId = $"{this.Id}";
        }

        public override bool Active => this.active;

        public override IChannelConfiguration Configuration { get; }

        public override ChannelMetadata Metadata { get; }

        public override bool Open => this.active && this.webSocket.State == WebSocketState.Open;

        public TaskCompletionSource<int> WebSocketClosed => this.webSocketClosed;

        internal bool ReadPending { get; set; }

        internal bool WriteInProgress { get; set; }

        protected override EndPoint LocalAddressInternal => throw new NotSupportedException();

        protected override EndPoint RemoteAddressInternal { get; }

        public void Dispose()
        {
            this.writeCancellationTokenSource.Dispose();
        }

        public ServerWebSocketChannel Option<T>(ChannelOption<T> option, T value)
        {
            Preconditions.CheckNotNull(option);

            this.Configuration.SetOption(option, value);
            return this;
        }

        protected override async void DoBeginRead()
        {
            IByteBuffer byteBuffer = null;
            IRecvByteBufAllocatorHandle allocHandle = null;
            bool close = false;
            try
            {
                if (!this.Open || this.ReadPending)
                {
                    return;
                }

                this.ReadPending = true;
                IByteBufferAllocator allocator = this.Configuration.Allocator;
                allocHandle = this.Configuration.RecvByteBufAllocator.NewHandle();
                allocHandle.Reset(this.Configuration);
                do
                {
                    byteBuffer = allocHandle.Allocate(allocator);
                    allocHandle.LastBytesRead = await this.DoReadBytes(byteBuffer);
                    if (allocHandle.LastBytesRead <= 0)
                    {
                        // nothing was read -> release the buffer.
                        byteBuffer.Release();
                        byteBuffer = null;
                        close = allocHandle.LastBytesRead < 0;
                        break;
                    }

                    this.Pipeline.FireChannelRead(byteBuffer);
                    allocHandle.IncMessagesRead(1);
                }
                while (allocHandle.ContinueReading());

                allocHandle.ReadComplete();
                this.ReadPending = false;
                this.Pipeline.FireChannelReadComplete();
            }
            catch (Exception e) when (!e.IsFatal())
            {
                Events.ReadException(this.correlationId, e);

                // Since this method returns void, all exceptions must be handled here.
                byteBuffer.SafeRelease();
                allocHandle?.ReadComplete();
                this.ReadPending = false;

                // WebSocket should change its state when an exception is thrown,
                // but it doesn't (see https://github.com/dotnet/corefx/issues/26219)
                // so we'll abort the connection here.
                this.Abort();

                this.Pipeline.FireChannelReadComplete();
                this.Pipeline.FireExceptionCaught(e);
                close = true;
            }

            if (close)
            {
                if (this.Active)
                {
                    await this.HandleCloseAsync();
                }
            }
        }

        protected override void DoBind(EndPoint localAddress)
        {
            throw new NotSupportedException("ServerWebSocketChannel does not support DoBind()");
        }

        protected override async void DoClose()
        {
            Events.TransportClosed(this.correlationId);

            this.active = false;
            WebSocketState webSocketState = this.webSocket.State;

            if (webSocketState != WebSocketState.Closed && webSocketState != WebSocketState.Aborted)
            {
                // Cancel any pending write
                this.CancelPendingWrite();

                try
                {
                    using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        await this.webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationTokenSource.Token);
                    }
                }
                catch (Exception e) when (!e.IsFatal())
                {
                    Events.CloseException(this.correlationId, e);
                    this.Abort();
                }
                finally
                {
                    this.webSocketClosed.TrySetResult(0);
                }
            }
        }

        protected override void DoDisconnect()
        {
            throw new NotSupportedException("ServerWebSocketChannel does not support DoDisconnect()");
        }

        protected override async void DoWrite(ChannelOutboundBuffer channelOutboundBuffer)
        {
            try
            {
                this.WriteInProgress = true;
                while (true)
                {
                    object currentMessage = channelOutboundBuffer.Current;
                    if (currentMessage == null)
                    {
                        // Wrote all messages.
                        break;
                    }

                    if (!(currentMessage is IByteBuffer byteBuffer))
                    {
                        throw new ArgumentException("channelOutBoundBuffer contents must be of type IByteBuffer");
                    }

                    if (byteBuffer.ReadableBytes == 0)
                    {
                        channelOutboundBuffer.Remove();
                        continue;
                    }

                    await this.webSocket.SendAsync(byteBuffer.GetIoBuffer(), WebSocketMessageType.Binary, true, this.writeCancellationTokenSource.Token);
                    channelOutboundBuffer.Remove();
                }

                this.WriteInProgress = false;
            }
            catch (Exception e) when (!e.IsFatal())
            {
                Events.WriteException(this.correlationId, e);
                this.WriteInProgress = false;
                this.Pipeline.FireExceptionCaught(e);
                await this.HandleCloseAsync();
            }
        }

        protected override bool IsCompatible(IEventLoop eventLoop) => true;

        protected override IChannelUnsafe NewUnsafe() => new WebSocketChannelUnsafe(this);

        void Abort()
        {
            try
            {
                Events.TransportAborted(this.correlationId);
                this.webSocket.Abort();
                this.webSocket.Dispose();
                this.writeCancellationTokenSource.Dispose();
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.TransportAbortFailedException(this.correlationId, ex);
            }
            finally
            {
                this.webSocketClosed.TrySetResult(0);
            }
        }

        void CancelPendingWrite()
        {
            try
            {
                this.writeCancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignore this error
            }
        }

        async Task<int> DoReadBytes(IByteBuffer byteBuffer)
        {
            WebSocketReceiveResult receiveResult = await this.webSocket.ReceiveAsync(
                new ArraySegment<byte>(byteBuffer.Array, byteBuffer.ArrayOffset + byteBuffer.WriterIndex, byteBuffer.WritableBytes),
                CancellationToken.None);

            if (receiveResult.MessageType == WebSocketMessageType.Text)
            {
                throw new ProtocolViolationException("Mqtt over WS message cannot be in text");
            }

            // Check if client closed WebSocket
            if (receiveResult.MessageType == WebSocketMessageType.Close)
            {
                return -1;
            }

            byteBuffer.SetWriterIndex(byteBuffer.WriterIndex + receiveResult.Count);
            return receiveResult.Count;
        }

        async Task HandleCloseAsync()
        {
            try
            {
                await this.CloseAsync();
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.CloseException(this.correlationId, ex);
                this.Abort();
            }
        }

        static class Events
        {
            const int IdStart = MqttEventIds.ServerWebSocketChannel;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ServerWebSocketChannel>();

            enum EventIds
            {
                TransportClosed = IdStart,
                CloseException,
                ReadException,
                WriteException,
                TransportAborted,
                TransportAbortFailedException
            }

            public static void CloseException(string correlationId, Exception ex)
            {
                Log.LogWarning((int)EventIds.CloseException, ex, Invariant($"Error closing transport. CorrelationId {correlationId}"));
            }

            public static void ReadException(string correlationId, Exception ex)
            {
                Log.LogWarning((int)EventIds.ReadException, ex, Invariant($"Error reading transport. CorrelationId {correlationId}"));
            }

            public static void TransportAborted(string correlationId)
            {
                Log.LogInformation((int)EventIds.TransportAborted, Invariant($"Transport aborted. CorrelationId {correlationId}"));
            }

            public static void TransportAbortFailedException(string correlationId, Exception ex)
            {
                Log.LogWarning((int)EventIds.TransportAbortFailedException, ex, Invariant($"Error aborting transport. CorrelationId {correlationId}"));
            }

            public static void TransportClosed(string correlationId)
            {
                Log.LogInformation((int)EventIds.TransportClosed, Invariant($"Transport closed. CorrelationId {correlationId}"));
            }

            public static void WriteException(string correlationId, Exception ex)
            {
                Log.LogWarning((int)EventIds.WriteException, ex, Invariant($"Error writing transport. CorrelationId {correlationId}"));
            }
        }

        class WebSocketChannelUnsafe : AbstractUnsafe
        {
            public WebSocketChannelUnsafe(AbstractChannel channel)
                : base(channel)
            {
            }

            public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                throw new NotSupportedException("ServerWebSocketChannel does not support BindAsync()");
            }

            protected override void Flush0()
            {
                // Flush immediately only when there's no pending flush.
                // If there's a pending flush operation, event loop will call FinishWrite() later,
                // and thus there's no need to call it now.
                if (((ServerWebSocketChannel)this.channel).WriteInProgress)
                {
                    return;
                }

                base.Flush0();
            }
        }
    }
}
