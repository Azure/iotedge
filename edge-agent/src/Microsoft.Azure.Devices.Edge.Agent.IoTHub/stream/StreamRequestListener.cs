// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
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

    public class StreamRequestListener : IStreamRequestListener, IDisposable
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
                await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, this.cts.Token);
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
}
