// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    using System;
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
                LogsStreamRequest streamRequest = await this.ReadLogsStreamingRequest(clientWebSocket, cancellationToken);
                Events.RequestData(streamRequest);

                var logOptions = new ModuleLogOptions(streamRequest.Id, LogsContentEncoding.None, LogsContentType.Text, ModuleLogFilter.Empty);
                using (var socketCancellationTokenSource = new CancellationTokenSource())
                {
                    Task ProcessLogsFrame(ArraySegment<byte> bytes)
                    {
                        if (clientWebSocket.State != WebSocketState.Open)
                        {
                            Events.WebSocketNotOpen(streamRequest.Id, clientWebSocket.State);
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
                }

                Events.StreamingCompleted(streamRequest.Id);
            }
            catch (Exception e)
            {
                Events.ErrorHandlingRequest(e);
            }
        }

        async Task<LogsStreamRequest> ReadLogsStreamingRequest(IClientWebSocket clientWebSocket, CancellationToken cancellationToken)
        {
            var buf = new byte[1024];
            var arrSeg = new ArraySegment<byte>(buf);
            WebSocketReceiveResult result = await clientWebSocket.ReceiveAsync(arrSeg, cancellationToken);
            if (result.Count > 0)
            {
                string jsonString = Encoding.UTF8.GetString(buf, 0, result.Count);
                return jsonString.FromJson<LogsStreamRequest>();
            }

            throw new InvalidOperationException("Did not receive logs request from server");
        }

        static class Events
        {
            const int IdStart = AgentEventIds.LogsStreamRequestHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<LogsStreamRequestHandler>();

            enum EventIds
            {
                ErrorHandlingRequest = IdStart,
                RequestData,
                StreamingCompleted,
                WebSocketNotOpen
            }

            public static void ErrorHandlingRequest(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingRequest, e, $"Error handling logs streaming request");
            }

            public static void RequestData(LogsStreamRequest streamRequest)
            {
                Log.LogInformation((int)EventIds.RequestData, $"Logs streaming request data - {streamRequest.ToJson()}");
            }

            public static void StreamingCompleted(string id)
            {
                Log.LogInformation((int)EventIds.StreamingCompleted, $"Completed streaming logs for {id}");
            }

            public static void WebSocketNotOpen(string id, WebSocketState state)
            {
                Log.LogInformation((int)EventIds.WebSocketNotOpen, $"Terminating streaming logs for {id} because WebSocket state is {state}");
            }
        }
    }
}
