// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    using System;
    using System.IO;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class LogsStreamRequestHandler : IStreamRequestHandler
    {
        readonly ILogsProvider logsProvider;

        public LogsStreamRequestHandler(ILogsProvider logsProvider)
        {
            this.logsProvider = Preconditions.CheckNotNull(logsProvider, nameof(logsProvider));
        }

        public async Task Handle(IClientWebSocket clientWebSocket, CancellationToken cancellationToken)
        {
            try
            {
                LogsStreamingRequest streamingRequest = await this.ReadLogsStreamingRequest(clientWebSocket, cancellationToken);
                Events.RequestData(streamingRequest);

                var logOptions = new ModuleLogOptions(streamingRequest.Id, LogsContentEncoding.None, LogsContentType.Text);
                var socketCancellationTokenSource = new CancellationTokenSource();
                Task ProcessLogsFrame(ArraySegment<byte> bytes)
                {
                    if (clientWebSocket.State != WebSocketState.Open)
                    {
                        Events.WebSocketNotOpen(streamingRequest.Id, clientWebSocket.State);
                        socketCancellationTokenSource.Cancel();
                        return Task.CompletedTask;
                    }
                    else
                    {
                        return clientWebSocket.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken);
                    }
                }
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, socketCancellationTokenSource.Token))
                {
                    await this.logsProvider.GetLogsStream(logOptions, ProcessLogsFrame, linkedCts.Token);
                }

                await this.WriteLogsStreamToOutput(streamingRequest.Id, clientWebSocket, stream, cancellationToken);
                Events.StreamingCompleted(streamingRequest.Id);
            }
            catch (Exception e)
            {
                Events.ErrorHandlingRequest(e);
            }
        }

        async Task WriteLogsStreamToOutput(string id, IClientWebSocket clientWebSocket, Stream stream, CancellationToken cancellationToken)
        {
            var buf = new byte[1024];
            try
            {
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Events.StreamingCancelled(id);
                        break;
                    }

                    if (clientWebSocket.State != WebSocketState.Open)
                    {
                        Events.WebSocketNotOpen(id, clientWebSocket.State);
                        break;
                    }

                    int count = await stream.ReadAsync(buf, 0, buf.Length, cancellationToken);
                    var arrSeg = new ArraySegment<byte>(buf, 0, count);
                    await clientWebSocket.SendAsync(arrSeg, WebSocketMessageType.Binary, true, cancellationToken);
                }
            }
            catch(Exception ex)
            {
                Events.ErrorWhileStreaming(id, ex);
            }
        }

        async Task<LogsStreamingRequest> ReadLogsStreamingRequest(IClientWebSocket clientWebSocket, CancellationToken cancellationToken)
        {
            var buf = new byte[1024];
            var arrSeg = new ArraySegment<byte>(buf);
            WebSocketReceiveResult result = await clientWebSocket.ReceiveAsync(arrSeg, cancellationToken);
            if (result.Count > 0)
            {
                string jsonString = Encoding.UTF8.GetString(buf, 0, result.Count);
                return jsonString.FromJson<LogsStreamingRequest>();
            }

            throw new InvalidOperationException("Did not receive logs request from server");
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<LogsStreamRequestHandler>();
            const int IdStart = AgentEventIds.LogsStreamRequestHandler;

            enum EventIds
            {
                ErrorHandlingRequest = IdStart,
                RequestData,
                ReceivedStream,
                StreamingCompleted,
                StreamingCancelled,
                WebSocketNotOpen,
                ErrorWhileStreaming
            }

            public static void ErrorHandlingRequest(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingRequest, e, $"Error handling logs streaming request");
            }

            public static void RequestData(LogsStreamingRequest streamingRequest)
            {
                Log.LogInformation((int)EventIds.RequestData, $"Logs streaming request data - {streamingRequest.ToJson()}");
            }

            public static void StreamingCompleted(string id)
            {
                Log.LogInformation((int)EventIds.StreamingCompleted, $"Completed streaming logs for {id}");
            }

            public static void StreamingCancelled(string id)
            {
                Log.LogInformation((int)EventIds.StreamingCancelled, $"Streaming logs for {id} cancelled.");
            }

            public static void WebSocketNotOpen(string id, WebSocketState state)
            {
                Log.LogInformation((int)EventIds.WebSocketNotOpen, $"Terminating streaming logs for {id} because WebSocket state is {state}");
            }

            public static void ErrorWhileStreaming(string id, Exception ex)
            {
                Log.LogInformation((int)EventIds.ErrorWhileStreaming, $"Error streaming logs for {id}, terminating streaming operation.");
                Log.LogDebug((int)EventIds.ErrorWhileStreaming, ex, $"Error streaming logs for {id}, terminating streaming operation.");
            }
        }
    }
}
