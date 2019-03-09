// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream
{
    using System;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

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
            Events.StartingPump();
            this.pumpTask = this.Pump(moduleClient);
        }

        public void Dispose()
        {
            this.cts.Cancel();
            Events.StoppingPump();
            // Don't dispose the cts here as it can cause an ObjectDisposedException.
        }

        static async Task<ClientWebSocket> GetStreamingClientAsync(DeviceStreamRequest streamRequest, CancellationToken cancellationToken)
        {
            var clientWebSocket = new ClientWebSocket();
            clientWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {streamRequest.AuthorizationToken}");
            await clientWebSocket.ConnectAsync(streamRequest.Url, cancellationToken);
            return clientWebSocket;
        }

        async Task<(string requestName, ClientWebSocket clientWebSocket)> WaitForStreamRequest(IModuleClient moduleClient, CancellationToken cancellationToken)
        {
            DeviceStreamRequest deviceStreamRequest = await moduleClient.WaitForDeviceStreamRequestAsync(cancellationToken);
            await moduleClient.AcceptDeviceStreamingRequest(deviceStreamRequest, cancellationToken);
            ClientWebSocket clientWebSocket = await GetStreamingClientAsync(deviceStreamRequest, cancellationToken);
            return (deviceStreamRequest.Name, clientWebSocket);
        }

        async Task Pump(IModuleClient moduleClient)
        {
            while (!this.cts.IsCancellationRequested)
            {
                try
                {
                    await this.streamLock.WaitAsync(this.cts.Token);
                    await this.ProcessStreamRequest(moduleClient);
                }
                catch (Exception e)
                {
                    Events.ErrorInPump(e);
                }
            }
        }

        async Task ProcessStreamRequest(IModuleClient moduleClient)
        {
            string requestName;
            ClientWebSocket clientWebSocket;
            try
            {
                (requestName, clientWebSocket) = await this.WaitForStreamRequest(moduleClient, this.cts.Token);
            }
            catch (Exception)
            {
                this.streamLock.Release();
                throw;
            }

            Events.NewStreamRequest(requestName, this.streamLock.CurrentCount);
            this.HandleRequest(requestName, clientWebSocket);
        }

        async void HandleRequest(string requestName, ClientWebSocket clientWebSocket)
        {
            try
            {
                if (this.streamRequestHandlerProvider.TryGetHandler(requestName, out IStreamRequestHandler handler))
                {
                    Events.HandlerFound(requestName);
                    await handler.Handle(clientWebSocket, this.cts.Token);
                    await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, this.cts.Token);
                }
                else
                {
                    Events.NoHandlerFound(requestName);
                    await clientWebSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, $"Unknown request name - {requestName}", this.cts.Token);
                }

                Events.RequestCompleted(requestName);
            }
            catch (Exception e)
            {
                Events.ErrorHandlingRequest(requestName, e);
            }
            finally
            {
                this.streamLock.Release();
            }
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<StreamRequestListener>();
            const int IdStart = AgentEventIds.StreamRequestListener;

            enum EventIds
            {
                ErrorHandlingRequest = IdStart,
                ErrorInPump,
                StoppingPump,
                StartingPump,
                NoHandlerFound,
                HandlerFound,
                NewStreamRequest,
                RequestCompleted
            }

            public static void ErrorHandlingRequest(string requestName, Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingRequest, e, $"Error handling streaming request for {requestName}");
            }

            public static void ErrorInPump(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorInPump, e, $"Error in stream request listener pump");
            }

            public static void RequestCompleted(string requestName)
            {
                Log.LogInformation((int)EventIds.RequestCompleted, $"Request for {requestName} completed");
            }

            public static void NewStreamRequest(string requestName, int streamLockCurrentCount)
            {
                Log.LogInformation((int)EventIds.NewStreamRequest, $"Received new stream request for {requestName}. Streams count = {streamLockCurrentCount}");
            }

            public static void HandlerFound(string requestName)
            {
                Log.LogInformation((int)EventIds.HandlerFound, $"Handling stream request for {requestName}.");
            }

            public static void NoHandlerFound(string requestName)
            {
                Log.LogWarning((int)EventIds.NoHandlerFound, $"No handler found for stream request {requestName}.");
            }

            public static void StartingPump()
            {
                Log.LogInformation((int)EventIds.StartingPump, "Starting stream request listener pump");
            }

            public static void StoppingPump()
            {
                Log.LogInformation((int)EventIds.StoppingPump, "Stopping stream request listener pump");
            }
        }
    }
}
