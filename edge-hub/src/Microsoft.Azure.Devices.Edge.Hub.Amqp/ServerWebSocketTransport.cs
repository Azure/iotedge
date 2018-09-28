// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.IO;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class ServerWebSocketTransport : TransportBase
    {
        readonly WebSocket webSocket;
        readonly string correlationId;
        readonly CancellationTokenSource cancellationTokenSource;
        bool socketAborted;

        public ServerWebSocketTransport(WebSocket webSocket, string localEndpoint, string remoteEndpoint, string correlationId)
            : base("serverwebsocket")
        {
            this.webSocket = Preconditions.CheckNotNull(webSocket, nameof(webSocket));
            this.LocalEndPoint = Preconditions.CheckNotNull(localEndpoint, nameof(localEndpoint));
            this.RemoteEndPoint = Preconditions.CheckNotNull(remoteEndpoint, nameof(remoteEndpoint));
            this.correlationId = Preconditions.CheckNonWhiteSpace(correlationId, nameof(correlationId));
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        public override string LocalEndPoint { get; }

        public override string RemoteEndPoint { get; }

        public override bool RequiresCompleteFrames => true;

        public override bool IsSecure => true;

        public override void SetMonitor(ITransportMonitor usageMeter)
        {
            // Don't do anything
        }

        public override bool WriteAsync(TransportAsyncCallbackArgs args)
        {
            Preconditions.CheckArgument(args.Buffer != null || args.ByteBufferList != null, $"{nameof(args.Buffer)} or {nameof(args.ByteBufferList)} should not be null, must have a buffer to write");
            Preconditions.CheckNotNull(args.CompletedCallback, nameof(args.CompletedCallback));

            this.ValidateIsOpen();

            args.Exception = null;
            Task taskResult = this.WriteAsyncCore(args);
            if (this.WriteTaskDone(taskResult, args))
            {
                return false;
            }

            taskResult.ToAsyncResult(this.OnWriteComplete, args);
            return true;
        }

        async Task WriteAsyncCore(TransportAsyncCallbackArgs args)
        {
            bool succeeded = false;
            try
            {
                if (args.Buffer != null)
                {
                    var arraySegment = new ArraySegment<byte>(args.Buffer, args.Offset, args.Count);
                    await this.webSocket.SendAsync(arraySegment, WebSocketMessageType.Binary, true, this.cancellationTokenSource.Token);
                }
                else
                {
                    foreach (ByteBuffer byteBuffer in args.ByteBufferList)
                    {
                        await this.webSocket.SendAsync(new ArraySegment<byte>(byteBuffer.Buffer, byteBuffer.Offset, byteBuffer.Length),
                            WebSocketMessageType.Binary, true, this.cancellationTokenSource.Token);
                    }
                }

                succeeded = true;
            }
            catch (WebSocketException webSocketException)
            {
                Events.WriteException(this.correlationId, webSocketException);
                throw new IOException(webSocketException.Message, webSocketException);
            }
            catch (TaskCanceledException taskCanceledException)
            {
                Events.WriteException(this.correlationId, taskCanceledException);
                throw new EdgeHubAmqpException(taskCanceledException.Message, ErrorCode.ServerError, taskCanceledException);
            }
            catch (Exception e) when (!e.IsFatal())
            {
                Events.WriteException(this.correlationId, e);
                throw;
            }
            finally
            {
                if (!succeeded)
                {
                    this.Abort();
                }
            }
        }

        public override bool ReadAsync(TransportAsyncCallbackArgs args)
        {
            Preconditions.CheckNotNull(args.Buffer, nameof(args.Buffer));
            Preconditions.CheckNotNull(args.CompletedCallback, nameof(args.CompletedCallback));

            this.ValidateIsOpen();
            this.ValidateBufferBounds(args.Buffer, args.Offset, args.Count);
            args.Exception = null;

            Task<int> taskResult = this.ReadAsyncCore(args);
            if (this.ReadTaskDone(taskResult, args))
            {
                return false;
            }

            taskResult.ContinueWith(_ =>
            {
                this.ReadTaskDone(taskResult, args);
                args.CompletedCallback(args);
            }, TaskContinuationOptions.ExecuteSynchronously);

            return true;
        }

        async Task<int> ReadAsyncCore(TransportAsyncCallbackArgs args)
        {
            bool succeeded = false;
            try
            {
                WebSocketReceiveResult receiveResult = await this.webSocket.ReceiveAsync(
                    new ArraySegment<byte>(args.Buffer, args.Offset, args.Count), this.cancellationTokenSource.Token);

                succeeded = true;
                return receiveResult.Count;
            }
            catch (Exception e) when (!e.IsFatal())
            {
                Events.ReadException(this.correlationId, e);
            }
            finally
            {
                if (!succeeded)
                {
                    this.Abort();
                }
            }

            // returning zero bytes will cause the Amqp layer above to clean up 
            return 0;
        }

        protected override bool OpenInternal()
        {
            this.ValidateIsOpen();

            return true;
        }

        protected override bool CloseInternal()
        {
            WebSocketState webSocketState = this.webSocket.State;
            if (webSocketState != WebSocketState.Closed && webSocketState != WebSocketState.Aborted)
            {
                Events.TransportClosed(this.correlationId);

                this.CloseInternalAsync(TimeSpan.FromSeconds(30)).ContinueWith(
                    t =>
                    {
                        Events.CloseException(this.correlationId, t.Exception);
                    }, TaskContinuationOptions.OnlyOnFaulted);

            }
            else
            {
                Events.TransportAlreadyClosedOrAborted(this.correlationId, webSocketState.ToString());
            }

            return true;
        }

        async Task CloseInternalAsync(TimeSpan timeout)
        {
            try
            {
                using (var tokenSource = new CancellationTokenSource(timeout))
                {
                    await this.webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, tokenSource.Token);
                }
            }
            catch (Exception e) when (!e.IsFatal())
            {
                Events.CloseException(this.correlationId, e);
            }

            // Call Abort anyway to ensure that all WebSocket Resources are released 
            this.Abort();
        }

        protected override void AbortInternal()
        {
            WebSocketState webSocketState = this.webSocket.State;
            if (!this.socketAborted && webSocketState != WebSocketState.Aborted)
            {
                Events.TransportAborted(this.correlationId);
                this.socketAborted = true;
                this.webSocket.Abort();
                this.webSocket.Dispose();
            }
            else
            {
                Events.TransportAlreadyClosedOrAborted(this.correlationId, webSocketState.ToString());
            }
        }

        bool ReadTaskDone(Task<int> taskResult, TransportAsyncCallbackArgs args)
        {
            IAsyncResult result = taskResult;
            args.BytesTransfered = 0;
            if (taskResult.IsFaulted)
            {
                args.Exception = taskResult.Exception;
                return true;
            }
            else if (taskResult.IsCompleted)
            {
                args.BytesTransfered = taskResult.Result;
                args.CompletedSynchronously = result.CompletedSynchronously;
                return true;
            }
            else if (taskResult.IsCanceled)  // This should not happen since TaskCanceledException is handled in ReadAsyncCore.
            {
                return true;
            }

            return false;
        }

        void OnWriteComplete(IAsyncResult result)
        {
            if (result.CompletedSynchronously)
            {
                return;
            }

            this.HandleWriteComplete(result);
        }

        void HandleWriteComplete(IAsyncResult result)
        {
            var taskResult = (Task)result;
            var args = (TransportAsyncCallbackArgs)taskResult.AsyncState;
            this.WriteTaskDone(taskResult, args);
            args.CompletedCallback(args);
        }

        bool WriteTaskDone(Task taskResult, TransportAsyncCallbackArgs args)
        {
            IAsyncResult result = taskResult;
            args.BytesTransfered = 0;
            if (taskResult.IsFaulted)
            {
                args.Exception = taskResult.Exception;
                return true;
            }
            else if (taskResult.IsCompleted)
            {
                args.BytesTransfered = args.Count;
                args.CompletedSynchronously = result.CompletedSynchronously;
                return true;
            }
            else if (taskResult.IsCanceled)  // This should not happen since TaskCanceledException is handled in WriteAsyncCore.
            {
                return true;
            }

            return false;
        }

        void ValidateIsOpen()
        {
            WebSocketState webSocketState = this.webSocket.State;
            switch (webSocketState)
            {
                case WebSocketState.Open:
                    return;
                case WebSocketState.Aborted:
                    throw new ObjectDisposedException(this.GetType().Name);
                case WebSocketState.Closed:
                case WebSocketState.CloseReceived:
                case WebSocketState.CloseSent:
                    throw new ObjectDisposedException(this.GetType().Name);
                default:
                    throw new AmqpException(AmqpErrorCode.IllegalState, null);
            }
        }

        void ValidateBufferBounds(byte[] buffer, int offset, int size)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be non-negative.");
            }

            if (offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, $"The specified offset exceeds the buffer size ({buffer.Length} bytes).");
            }

            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "Size must be non-negative");
            }

            int remainingBufferSpace = buffer.Length - offset;
            if (size > remainingBufferSpace)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, $"The specified size exceeds the remaining buffer space ({remainingBufferSpace} bytes).");
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<ServerWebSocketTransport>();
            const int IdStart = AmqpEventIds.ServerWebSocketTransport;

            enum EventIds
            {
                WriteException = IdStart,
                ReadException,
                TransportClosed,
                TransportAlreadyClosedOrAborted,
                CloseException,
                TransportAborted
            }

            public static void WriteException(string correlationId, Exception ex)
            {
                Log.LogWarning((int)EventIds.WriteException, ex, $"Websockets write failed CorrelationId {correlationId}");
            }

            public static void ReadException(string correlationId, Exception ex)
            {
                Log.LogWarning((int)EventIds.ReadException, ex, $"Websockets read failed CorrelationId {correlationId}");
            }

            public static void TransportClosed(string correlationId)
            {
                Log.LogInformation((int)EventIds.TransportClosed, $"Connection closed CorrelationId {correlationId}");
            }

            internal static void TransportAlreadyClosedOrAborted(string correlationId, string state)
            {
                Log.LogInformation((int)EventIds.TransportAlreadyClosedOrAborted, $"Connection already closed or aborted CorrelationId {correlationId} State {state}");
            }

            public static void CloseException(string correlationId, Exception ex)
            {
                Log.LogWarning((int)EventIds.CloseException, ex, $"Websockets close failed CorrelationId {correlationId}");
            }

            public static void TransportAborted(string correlationId)
            {
                Log.LogInformation((int)EventIds.TransportAborted, $"Connection aborted CorrelationId {correlationId}");
            }
        }
    }
}
