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

    class HttpUdsMessageHandler : HttpMessageHandler
    {
        readonly Uri providerUri;

        public HttpUdsMessageHandler(Uri providerUri)
        {
            this.providerUri = providerUri;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var endpoint = new UnixDomainSocketEndPoint(this.providerUri.LocalPath);

            Events.Connecting(this.providerUri.LocalPath);
            // do not dispose `Socket` or `HttpBufferedStream` here, b/c it will be used later
            // by the consumer of HttpResponseMessage (HttpResponseMessage.Content.ReadAsStringAsync()).
            // When HttpResponseMessage is disposed - the stream and socket is disposed as well.
            Socket socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(endpoint);
            Events.Connected(this.providerUri.LocalPath);

            var stream = new HttpBufferedStream(new NetworkStream(socket, true));
            var serializer = new HttpRequestResponseSerializer();
            byte[] requestBytes = serializer.SerializeRequest(request);

            Events.SendRequest(request.RequestUri);
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length, cancellationToken);
            if (request.Content != null)
            {
                await request.Content.CopyToAsync(stream);
            }

            HttpResponseMessage response = await serializer.DeserializeResponse(stream, cancellationToken);
            Events.ResponseReceived(response.StatusCode);

            return response;
        }

        static class Events
        {
            const int IdStart = UtilEventsIds.HttpUdsMessageHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<HttpUdsMessageHandler>();

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
