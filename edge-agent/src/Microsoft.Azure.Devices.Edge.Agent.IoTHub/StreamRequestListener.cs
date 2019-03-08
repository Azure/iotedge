// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.IO;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class StreamRequestListener : IDisposable
    {
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly IStreamRequestHandlerProvider streamRequestHandlerProvider;
        readonly SemaphoreSlim streamLock;
        Task pumpTask;

        public StreamRequestListener(IStreamRequestHandlerProvider streamRequestHandlerProvider, int maxConcurrentStreams = 10)
        {
            this.streamRequestHandlerProvider = Preconditions.CheckNotNull(streamRequestHandlerProvider, nameof(streamRequestHandlerProvider));
            this.streamLock = new SemaphoreSlim(maxConcurrentStreams);
        }

        public void InitPump(IModuleClient moduleClient)
        {
            this.pumpTask = this.Pump(moduleClient);
        }

        async Task Pump(IModuleClient moduleClient)
        {
            while (!this.cts.IsCancellationRequested)
            {
                await this.streamLock.WaitAsync(this.cts.Token);
                (string requestName, ClientWebSocket clientWebSocket) = await this.WaitForStreamRequest(moduleClient);
                this.HandleRequest(requestName, clientWebSocket);
            }
        }

        async void HandleRequest(string requestName, ClientWebSocket clientWebSocket)
        {
            if (this.streamRequestHandlerProvider.TryGetHandler(requestName, out IStreamRequestHandler handler))
            {
                await handler.Handle(clientWebSocket, this.cts.Token);
            }
            else
            {
                await clientWebSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, $"Unknown request name - {requestName}", this.cts.Token);
            }

            this.streamLock.Release();
        }

        async Task<(string requestName, ClientWebSocket clientWebSocket)> WaitForStreamRequest(IModuleClient moduleClient)
        {
            DeviceStreamRequest deviceStreamRequest = await moduleClient.WaitForDeviceStreamRequestAsync(this.cts.Token);
            await moduleClient.AcceptDeviceStreamingRequest(deviceStreamRequest, this.cts.Token);
            ClientWebSocket clientWebSocket = await GetStreamingClientAsync(deviceStreamRequest, this.cts.Token);
            return (deviceStreamRequest.Name, clientWebSocket);
        }

        public static async Task<ClientWebSocket> GetStreamingClientAsync(DeviceStreamRequest streamRequest, CancellationToken cancellationToken)
        {
            ClientWebSocket wsClient = new ClientWebSocket();
            wsClient.Options.SetRequestHeader("Authorization", $"Bearer {streamRequest.AuthorizationToken}");
            await wsClient.ConnectAsync(streamRequest.Url, cancellationToken).ConfigureAwait(false);
            return wsClient;
        }

        public void Dispose()
        {
            this.cts.Cancel();
            this.cts?.Dispose();
        }
    }

    public interface IStreamRequestHandlerProvider
    {
        bool TryGetHandler(string requestName, out IStreamRequestHandler handler);
    }

    public class StreamRequestHandlerProvider : IStreamRequestHandlerProvider
    {
        const string LogsStreamName = "Logs";
        readonly IRuntimeInfoProvider runtimeInfoProvider;

        public StreamRequestHandlerProvider(IRuntimeInfoProvider runtimeInfoProvider)
        {
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
        }

        public bool TryGetHandler(string requestName, out IStreamRequestHandler handler)
        {
            if (requestName.Equals(LogsStreamName, StringComparison.OrdinalIgnoreCase))
            {
                handler = new LogsStreamRequestHandler(this.runtimeInfoProvider);
                return true;
            }

            handler = null;
            return false;
        }
    }

    public interface IStreamRequestHandler
    {
        Task Handle(ClientWebSocket clientWebSocket, CancellationToken cancellationToken);
    }

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
            while (!cancellationToken.IsCancellationRequested)
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

    public class LogsStreamingRequest
    {
        public LogsStreamingRequest(string schemaVersion, string id)
        {
            this.SchemaVersion = schemaVersion;
            this.Id = id;
        }

        public string SchemaVersion { get; }
        public string Id { get; }
    }
}
