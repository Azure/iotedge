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
            Option<(string requestName, IClientWebSocket clientWebSocket)> result = await this.WaitForStreamRequest(moduleClient);
            // If we don't get a valid stream, release the lock.
            if (!result.HasValue)
            {
                this.streamLock.Release();
            }
            else
            {
                // We got a WebSocket stream. Pass it to a handler on a background thread. This thread is
                // responsible for releasing the lock after it completes.
                result.ForEach(
                    r =>
                    {
                        Events.NewStreamRequest(r.requestName, this.streamLock.CurrentCount);
                        this.HandleRequest(r.requestName, r.clientWebSocket);
                    });
            }
        }

        async Task<Option<(string requestName, IClientWebSocket clientWebSocket)>> WaitForStreamRequest(IModuleClient moduleClient)
        {
            var result = Option.None<(string, IClientWebSocket)>();
            try
            {
                DeviceStreamRequest deviceStreamRequest = await moduleClient.WaitForDeviceStreamRequestAsync(this.cts.Token);
                if (deviceStreamRequest != null)
                {
                    IClientWebSocket clientWebSocket = await moduleClient.AcceptDeviceStreamingRequestAndConnect(deviceStreamRequest, this.cts.Token);
                    result = Option.Some((deviceStreamRequest.Name, clientWebSocket));
                }
            }
            catch (Exception ex)
            {
                Events.ErrorGettingStream(ex);
            }

            return result;
        }

        async void HandleRequest(string requestName, IClientWebSocket clientWebSocket)
        {
            try
            {
                if (this.streamRequestHandlerProvider.TryGetHandler(requestName, out IStreamRequestHandler handler))
                {
                    Events.HandlerFound(requestName);
                    await handler.Handle(clientWebSocket, this.cts.Token);
                    await this.CloseWebSocketConnection(requestName, clientWebSocket, WebSocketCloseStatus.NormalClosure, string.Empty);
                }
                else
                {
                    Events.NoHandlerFound(requestName);
                    await this.CloseWebSocketConnection(requestName, clientWebSocket, WebSocketCloseStatus.InvalidPayloadData, $"Unknown request name - {requestName}");
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

        async Task CloseWebSocketConnection(string requestName, IClientWebSocket clientWebSocket, WebSocketCloseStatus webSocketCloseStatus, string message)
        {
            try
            {
                await clientWebSocket.CloseAsync(webSocketCloseStatus, message, this.cts.Token);
            }
            catch (Exception ex)
            {
                Events.ErrorClosingWebSocket(requestName, ex);
            }
        }

        static class Events
        {
            const int IdStart = AgentEventIds.StreamRequestListener;
            static readonly ILogger Log = Logger.Factory.CreateLogger<StreamRequestListener>();

            enum EventIds
            {
                ErrorHandlingRequest = IdStart,
                ErrorInPump,
                StoppingPump,
                StartingPump,
                NoHandlerFound,
                HandlerFound,
                NewStreamRequest,
                RequestCompleted,
                ErrorGettingStream,
                ErrorClosingWebSocket
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

            public static void ErrorGettingStream(Exception ex)
            {
                Log.LogWarning((int)EventIds.ErrorGettingStream, "Stream request was not processed successfully.");
                Log.LogDebug((int)EventIds.ErrorGettingStream, ex, "Error accepting stream request");
            }

            internal static void ErrorClosingWebSocket(string requestName, Exception ex)
            {
                Log.LogInformation((int)EventIds.ErrorClosingWebSocket, $"Error closing WebSocketClient for request {requestName} - {ex.Message}.");
            }
        }
    }
}
