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
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LogsStreamRequestHandler : IStreamRequestHandler
    {
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public LogsStreamRequestHandler(IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
        }

        public async Task Handle(ClientWebSocket clientWebSocket, CancellationToken cancellationToken)
        {
            LogsStreamingRequest streamingRequest = await this.ReadLogsStreamingRequest(clientWebSocket, cancellationToken);
            Stream stream = await this.runtimeInfoProvider.GetModuleLogs(streamingRequest.Id, true, Option.None<int>(), cancellationToken);
            await this.WriteLogsStreamToOutput(clientWebSocket, stream, cancellationToken);
        }

        async Task WriteLogsStreamToOutput(ClientWebSocket clientWebSocket, Stream stream, CancellationToken cancellationToken)
        {
            byte[] buf = new byte[1024];
            while (!cancellationToken.IsCancellationRequested && clientWebSocket.State == WebSocketState.Open)
            {
                int count = await stream.ReadAsync(buf, 0, buf.Length, cancellationToken);
                var arrSeg = new ArraySegment<byte>(buf, 0, count);
                await clientWebSocket.SendAsync(arrSeg, WebSocketMessageType.Binary, false, cancellationToken);
            }
        }

        async Task<LogsStreamingRequest> ReadLogsStreamingRequest(ClientWebSocket clientWebSocket, CancellationToken cancellationToken)
        {
            var buf = new byte[1024];
            var arrSeg = new ArraySegment<byte>(buf);
            WebSocketReceiveResult result = await clientWebSocket.ReceiveAsync(arrSeg, cancellationToken);
            if (result.Count > 0)
            {
                string jsonString = Encoding.UTF8.GetString(buf, 0, result.Count);
                return jsonString.FromJson<LogsStreamingRequest>();
            }

            throw new InvalidOperationException("Did not receive enough bytes from socket");
        }
    }
}
