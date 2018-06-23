// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Uds
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    internal class HttpUdsMessageHandler : HttpMessageHandler
    {
        private readonly Uri providerUri;

        public HttpUdsMessageHandler(Uri providerUri)
        {
            this.providerUri = providerUri;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Socket socket = await this.GetConnectedSocketAsync().ConfigureAwait(false);
            var stream = new HttpBufferedStream(new NetworkStream(socket, true));

            var serializer = new HttpRequestResponseSerializer();
            byte[] requestBytes = serializer.SerializeRequest(request);

            Events.SendRequest(request.RequestUri);
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length, cancellationToken).ConfigureAwait(false);
            if (request.Content != null)
            {
                await request.Content.CopyToAsync(stream).ConfigureAwait(false);
            }

            HttpResponseMessage response = await serializer.DeserializeResponse(stream, cancellationToken).ConfigureAwait(false);
            Events.ResponseReceived(response.StatusCode);

            return response;
        }

        private async Task<Socket> GetConnectedSocketAsync()
        {
            var endpoint = new UnixDomainSocketEndPoint(this.providerUri.LocalPath);
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            Events.Connecting(this.providerUri.LocalPath);
            await socket.ConnectAsync(endpoint).ConfigureAwait(false);
            Events.Connected(this.providerUri.LocalPath);

            return socket;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<HttpUdsMessageHandler>();
            const int IdStart = UtilEventsIds.HttpUdsMessageHandler;

            enum EventIds
            {
                Connecting = IdStart,
                Connected,
                SendRequest,
                ReceivedResponse
            }

            internal static void Connecting(string url)
            {
                Log.LogDebug((int)EventIds.Connecting, $"Connecting socket {url}");
            }

            internal static void Connected(string url)
            {
                Log.LogDebug((int)EventIds.Connected, $"Connected socket {url}");
            }

            internal static void SendRequest(Uri requestUri)
            {
                Log.LogDebug((int)EventIds.SendRequest, $"Sending request {requestUri}");
            }

            internal static void ResponseReceived(HttpStatusCode statusCode)
            {
                Log.LogDebug((int)EventIds.ReceivedResponse, $"Response received {statusCode}");
            }
        }
    }
}
