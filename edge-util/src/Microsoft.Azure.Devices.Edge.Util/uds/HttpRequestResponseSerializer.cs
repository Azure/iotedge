// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Uds
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    class HttpRequestResponseSerializer
    {
        const char SP = ' ';
        const char CR = '\r';
        const char LF = '\n';
        const char ProtocolVersionSeparator = '/';
        const string Protocol = "HTTP";
        const char HeaderSeparator = ':';
        const string ContentLengthHeaderName = "content-length";

        public byte[] SerializeRequest(HttpRequestMessage request)
        {
            Preconditions.CheckNotNull(request, nameof(request));
            Preconditions.CheckNotNull(request.RequestUri, nameof(request.RequestUri));

            this.PreProcessRequest(request);

            var builder = new StringBuilder();
            // request-line   = method SP request-target SP HTTP-version CRLF
            builder.Append(request.Method);
            builder.Append(SP);
            builder.Append(request.RequestUri.IsAbsoluteUri ? request.RequestUri.PathAndQuery : Uri.EscapeUriString(request.RequestUri.ToString()));
            builder.Append(SP);
            builder.Append($"{Protocol}{ProtocolVersionSeparator}");
            builder.Append(new Version(1, 1).ToString(2));
            builder.Append(CR);
            builder.Append(LF);

            // Headers
            builder.Append(request.Headers);

            if (request.Content != null)
            {
                long? contentLength = request.Content.Headers.ContentLength;
                if (contentLength.HasValue)
                {
                    request.Content.Headers.ContentLength = contentLength.Value;
                }

                builder.Append(request.Content.Headers);
            }

            // Headers end
            builder.Append(CR);
            builder.Append(LF);

            return Encoding.ASCII.GetBytes(builder.ToString());
        }

        public async Task<HttpResponseMessage> DeserializeResponse(HttpBufferedStream bufferedStream, CancellationToken cancellationToken)
        {
            var httpResponse = new HttpResponseMessage();

            await this.SetResponseStatusLine(httpResponse, bufferedStream, cancellationToken);
            await this.SetHeadersAndContent(httpResponse, bufferedStream, cancellationToken);

            return httpResponse;
        }

        async Task SetHeadersAndContent(HttpResponseMessage httpResponse, HttpBufferedStream bufferedStream, CancellationToken cancellationToken)
        {
            IList<string> headers = new List<string>();
            string line = await bufferedStream.ReadLineAsync(cancellationToken);
            while (!string.IsNullOrWhiteSpace(line))
            {
                headers.Add(line);
                line = await bufferedStream.ReadLineAsync(cancellationToken);
            }

            httpResponse.Content = new StreamContent(bufferedStream);
            foreach (string header in headers)
            {
                if (string.IsNullOrWhiteSpace(header))
                {
                    // headers end
                    break;
                }

                int headerSeparatorPosition = header.IndexOf(HeaderSeparator);
                if (headerSeparatorPosition <= 0)
                {
                    throw new HttpRequestException($"Header is invalid {header}.");
                }

                string headerName = header.Substring(0, headerSeparatorPosition).Trim();
                string headerValue = header.Substring(headerSeparatorPosition + 1).Trim();

                bool headerAdded = httpResponse.Headers.TryAddWithoutValidation(headerName, headerValue);
                if (!headerAdded)
                {
                    if (string.Equals(headerName, ContentLengthHeaderName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!long.TryParse(headerValue, out long contentLength))
                        {
                            throw new HttpRequestException($"Header value is invalid for {headerName}.");
                        }

                        await httpResponse.Content.LoadIntoBufferAsync(contentLength);
                    }

                    httpResponse.Content.Headers.TryAddWithoutValidation(headerName, headerValue);
                }
            }
        }

        async Task SetResponseStatusLine(HttpResponseMessage httpResponse, HttpBufferedStream bufferedStream, CancellationToken cancellationToken)
        {
            string statusLine = await bufferedStream.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(statusLine))
            {
                throw new HttpRequestException("Response is empty.");
            }

            string[] statusLineParts = statusLine.Split(new[] { SP }, 3);
            if (statusLineParts.Length < 3)
            {
                throw new HttpRequestException("Status line is not valid.");
            }

            string[] httpVersion = statusLineParts[0].Split(new[] { ProtocolVersionSeparator }, 2);
            if (httpVersion.Length < 2 || !Version.TryParse(httpVersion[1], out Version versionNumber))
            {
                throw new HttpRequestException($"Version is not valid {statusLineParts[0]}.");
            }

            httpResponse.Version = versionNumber;

            if (!Enum.TryParse(statusLineParts[1], out HttpStatusCode statusCode))
            {
                throw new HttpRequestException($"StatusCode is not valid {statusLineParts[1]}.");
            }

            httpResponse.StatusCode = statusCode;
            httpResponse.ReasonPhrase = statusLineParts[2];
        }

        void PreProcessRequest(HttpRequestMessage request)
        {
            if (string.IsNullOrEmpty(request.Headers.Host))
            {
                request.Headers.Host = $"{request.RequestUri.DnsSafeHost}:{request.RequestUri.Port}";
            }

            request.Headers.ConnectionClose = true;
        }
    }
}
