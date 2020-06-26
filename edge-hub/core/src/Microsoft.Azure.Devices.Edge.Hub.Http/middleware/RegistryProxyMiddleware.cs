// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Middleware
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RegistryProxyMiddleware
    {
        // This pattern is used to match registry API for create, update, get, delete of a module, and list modules.
        static readonly Regex registryApiUriPattern = new Regex(@"\/devices\/[^\/]+\/modules(\/[^\/]+)?$");
        static readonly HttpClient httpClient = new HttpClient();
        readonly RequestDelegate nextMiddleware;
        readonly string gatewayHostname;

        public RegistryProxyMiddleware(RequestDelegate nextMiddleware, string gatewayHostname)
        {
            this.nextMiddleware = nextMiddleware;
            this.gatewayHostname = Preconditions.CheckNonWhiteSpace(gatewayHostname, nameof(gatewayHostname));
        }

        public async Task Invoke(HttpContext context)
        {
            Option<Uri> destinationUri = this.BuildDestinationUri(context);

            await destinationUri.ForEachAsync(
                async uri =>
                {
                    HttpRequestMessage proxyRequestMessage = this.CreateProxyRequestMessage(context, uri);

                    using (HttpResponseMessage responseMessage = await httpClient.SendAsync(proxyRequestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted))
                    {
                        await this.CopyProxyResponseAsync(context, responseMessage);
                    }
                },
                async () =>
                {
                    await this.nextMiddleware(context);
                });
        }

        internal HttpRequestMessage CreateProxyRequestMessage(HttpContext context, Uri destinationUri)
        {
            HttpRequest request = context.Request;
            var requestMessage = new HttpRequestMessage();
            string requestMethod = request.Method;

            if (!HttpMethods.IsGet(requestMethod) &&
                !HttpMethods.IsHead(requestMethod) &&
                !HttpMethods.IsDelete(requestMethod) &&
                !HttpMethods.IsTrace(requestMethod))
            {
                var streamContent = new StreamContent(request.Body);
                requestMessage.Content = streamContent;
            }

            foreach (var header in request.Headers)
            {
                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && requestMessage.Content != null)
                {
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            requestMessage.RequestUri = destinationUri;
            requestMessage.Headers.Host = destinationUri.Authority;
            requestMessage.Method = new HttpMethod(requestMethod);

            return requestMessage;
        }

        internal async Task CopyProxyResponseAsync(HttpContext context, HttpResponseMessage responseMessage)
        {
            if (responseMessage == null)
            {
                throw new ArgumentNullException(nameof(responseMessage));
            }

            HttpResponse response = context.Response;

            response.StatusCode = (int)responseMessage.StatusCode;
            foreach (var header in responseMessage.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }

            foreach (var header in responseMessage.Content.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }

            // SendAsync removes chunking from the response. This removes the header so it doesn't expect a chunked response.
            response.Headers.Remove("transfer-encoding");

            await responseMessage.Content.CopyToAsync(response.Body);
        }

        internal Option<Uri> BuildDestinationUri(HttpContext context)
        {
            HttpRequest request = context.Request;

            if (context.WebSockets.IsWebSocketRequest ||
                !request.IsHttps ||
                !registryApiUriPattern.IsMatch(request.Path))
            {
                return Option.None<Uri>();
            }

            var destinationUriBuilder = new UriBuilder(request.GetEncodedUrl());
            destinationUriBuilder.Host = this.gatewayHostname;
            return Option.Some(destinationUriBuilder.Uri);
        }
    }
}
