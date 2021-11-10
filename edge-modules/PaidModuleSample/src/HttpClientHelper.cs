// Copyright (c) Microsoft. All rights reserved.
namespace PaidModuleSample
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class HttpClientHelper
    {
        const string HttpScheme = "http";
        const string HttpsScheme = "https";
        const string UnixScheme = "unix";

        public static HttpClient GetHttpClient(Uri serverUri)
        {
            HttpClient client;

            if (serverUri.Scheme.Equals(HttpScheme, StringComparison.OrdinalIgnoreCase) || serverUri.Scheme.Equals(HttpsScheme, StringComparison.OrdinalIgnoreCase))
            {
                client = new HttpClient();
                return client;
            }

            if (serverUri.Scheme.Equals(UnixScheme, StringComparison.OrdinalIgnoreCase))
            {
                client = new HttpClient(new HttpUdsMessageHandler(serverUri));
                return client;
            }

            throw new InvalidOperationException("ProviderUri scheme is not supported");
        }

        public static string GetBaseUrl(Uri serverUri)
        {
            if (serverUri.Scheme.Equals(UnixScheme, StringComparison.OrdinalIgnoreCase))
            {
                return $"{HttpScheme}://{serverUri.Segments.Last()}";
            }

            return serverUri.OriginalString;
        }
    }
    
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

            // do not dispose `Socket` or `HttpBufferedStream` here, b/c it will be used later
            // by the consumer of HttpResponseMessage (HttpResponseMessage.Content.ReadAsStringAsync()).
            // When HttpResponseMessage is disposed - the stream and socket is disposed as well.
            Socket socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(endpoint);

            var stream = new HttpBufferedStream(new NetworkStream(socket, true));
            var serializer = new HttpRequestResponseSerializer();
            byte[] requestBytes = serializer.SerializeRequest(request);

            await stream.WriteAsync(requestBytes, 0, requestBytes.Length, cancellationToken);
            if (request.Content != null)
            {
                await request.Content.CopyToAsync(stream);
            }

            HttpResponseMessage response = await serializer.DeserializeResponse(stream, cancellationToken);

            return response;
        }
    }

    class HttpBufferedStream : Stream
    {
        const char CR = '\r';
        const char LF = '\n';
        readonly BufferedStream innerStream;

        public HttpBufferedStream(Stream stream)
        {
            this.innerStream = new BufferedStream(stream);
        }

        public override bool CanRead => this.innerStream.CanRead;

        public override bool CanSeek => this.innerStream.CanSeek;

        public override bool CanWrite => this.innerStream.CanWrite;

        public override long Length => this.innerStream.Length;

        public override long Position
        {
            get => this.innerStream.Position;
            set => this.innerStream.Position = value;
        }

        public override void Flush()
        {
            this.innerStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return this.innerStream.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.innerStream.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            int position = 0;
            var buffer = new byte[1];
            bool crFound = false;
            var builder = new StringBuilder();
            while (true)
            {
                int length = await this.innerStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                if (length == 0)
                {
                    throw new IOException("Unexpected end of stream.");
                }

                if (crFound && (char)buffer[position] == LF)
                {
                    builder.Remove(builder.Length - 1, 1);
                    return builder.ToString();
                }

                builder.Append((char)buffer[position]);
                crFound = (char)buffer[position] == CR;
            }
        }

        public string ReadLine()
        {
            int position = 0;
            var buffer = new byte[1];
            bool crFound = false;
            var builder = new StringBuilder();
            while (true)
            {
                int length = this.innerStream.Read(buffer, 0, buffer.Length);
                if (length == 0)
                {
                    throw new IOException("Unexpected end of stream.");
                }

                if (crFound && (char)buffer[position] == LF)
                {
                    builder.Remove(builder.Length - 1, 1);
                    return builder.ToString();
                }

                builder.Append((char)buffer[position]);
                crFound = (char)buffer[position] == CR;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.innerStream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return this.innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            this.innerStream.Dispose();
        }
    }

    class HttpChunkedStreamReader : Stream
    {
        readonly HttpBufferedStream stream;
        int chunkBytes;
        bool eos;

        public HttpChunkedStreamReader(HttpBufferedStream stream)
        {
            this.stream = stream;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (this.eos)
            {
                return 0;
            }

            if (this.chunkBytes == 0)
            {
                string line = await this.stream.ReadLineAsync(cancellationToken);
                if (!int.TryParse(line, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out this.chunkBytes))
                {
                    throw new IOException($"Cannot parse chunk header - {line}");
                }
            }

            int bytesRead = 0;
            if (this.chunkBytes > 0)
            {
                int bytesToRead = Math.Min(count, this.chunkBytes);
                bytesRead = await this.stream.ReadAsync(buffer, offset, bytesToRead, cancellationToken);
                if (bytesToRead == 0)
                {
                    throw new EndOfStreamException();
                }

                this.chunkBytes -= bytesToRead;
            }

            if (this.chunkBytes == 0)
            {
                await this.stream.ReadLineAsync(cancellationToken);
                if (bytesRead == 0)
                {
                    this.eos = true;
                }
            }

            return bytesRead;
        }

        public override void Flush() => throw new NotImplementedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.eos)
            {
                return 0;
            }

            if (this.chunkBytes == 0)
            {
                string line = this.stream.ReadLine();
                if (!int.TryParse(line, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out this.chunkBytes))
                {
                    throw new IOException($"Cannot parse chunk header - {line}");
                }
            }

            int bytesRead = 0;
            if (this.chunkBytes > 0)
            {
                int bytesToRead = Math.Min(count, this.chunkBytes);
                bytesRead = this.stream.Read(buffer, offset, bytesToRead);
                if (bytesToRead == 0)
                {
                    throw new EndOfStreamException();
                }

                this.chunkBytes -= bytesToRead;
            }

            if (this.chunkBytes == 0)
            {
                this.stream.ReadLine();
                if (bytesRead == 0)
                {
                    this.eos = true;
                }
            }

            return bytesRead;
        }

        // Underlying Stream does not support Seek()
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            this.stream.Dispose();
        }
    }

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
            var contentHeaders = new Dictionary<string, string>();
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
                    contentHeaders.Add(headerName, headerValue);
                }
            }

            bool isChunked = httpResponse.Headers.TransferEncodingChunked.HasValue
                             && httpResponse.Headers.TransferEncodingChunked.Value;

            httpResponse.Content = isChunked
                ? new StreamContent(new HttpChunkedStreamReader(bufferedStream))
                : new StreamContent(bufferedStream);

            foreach (KeyValuePair<string, string> contentHeader in contentHeaders)
            {
                httpResponse.Content.Headers.TryAddWithoutValidation(contentHeader.Key, contentHeader.Value);
                if (string.Equals(contentHeader.Key, ContentLengthHeaderName, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!long.TryParse(contentHeader.Value, out long contentLength))
                    {
                        throw new HttpRequestException($"Header value {contentHeader.Value} is invalid for {ContentLengthHeaderName}.");
                    }

                    await httpResponse.Content.LoadIntoBufferAsync(contentLength);
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

            if (!Enum.TryParse(statusLineParts[1], out HttpStatusCode statusCode) || !Enum.IsDefined(typeof(HttpStatusCode), statusCode))
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

    public partial class HttpWorkloadClient
    {
        private string _baseUrl = "http://";
        private System.Net.Http.HttpClient _httpClient;
        private System.Lazy<Newtonsoft.Json.JsonSerializerSettings> _settings;

        public HttpWorkloadClient(System.Net.Http.HttpClient httpClient)
        {
            _httpClient = httpClient;
            _settings = new System.Lazy<Newtonsoft.Json.JsonSerializerSettings>(() =>
            {
                var settings = new Newtonsoft.Json.JsonSerializerSettings();
                return settings;
            });
        }

        public string BaseUrl
        {
            get { return _baseUrl; }
            set { _baseUrl = value; }
        }

        protected Newtonsoft.Json.JsonSerializerSettings JsonSerializerSettings { get { return _settings.Value; } }


        public System.Threading.Tasks.Task<SignResponse> SignAsync(string api_version, string name, string genid, SignRequest payload)
        {
            return SignAsync(api_version, name, genid, payload, System.Threading.CancellationToken.None);
        }

        public async System.Threading.Tasks.Task<SignResponse> SignAsync(string api_version, string name, string genid, SignRequest payload, System.Threading.CancellationToken cancellationToken)
        {
            if (name == null)
                throw new System.ArgumentNullException("name");

            if (genid == null)
                throw new System.ArgumentNullException("genid");

            if (api_version == null)
                throw new System.ArgumentNullException("api_version");

            var urlBuilder_ = new System.Text.StringBuilder();
            urlBuilder_.Append(BaseUrl != null ? BaseUrl.TrimEnd('/') : "").Append("/modules/{name}/genid/{genid}/sign?");
            urlBuilder_.Replace("{name}", System.Net.WebUtility.UrlEncode(ConvertToString(name, System.Globalization.CultureInfo.InvariantCulture)));
            urlBuilder_.Replace("{genid}", System.Net.WebUtility.UrlEncode(ConvertToString(genid, System.Globalization.CultureInfo.InvariantCulture)));
            urlBuilder_.Append("api-version=").Append(System.Net.WebUtility.UrlEncode(ConvertToString(api_version, System.Globalization.CultureInfo.InvariantCulture))).Append("&");
            urlBuilder_.Length--;

            var client_ = _httpClient;
            try
            {
                using (var request_ = new System.Net.Http.HttpRequestMessage())
                {
                    var content_ = new System.Net.Http.StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload, _settings.Value));
                    content_.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json");
                    request_.Content = content_;
                    request_.Method = new System.Net.Http.HttpMethod("POST");
                    request_.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var url_ = urlBuilder_.ToString();
                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
                        if (response_.Content != null && response_.Content.Headers != null)
                        {
                            foreach (var item_ in response_.Content.Headers)
                                headers_[item_.Key] = item_.Value;
                        }


                        var status_ = ((int)response_.StatusCode).ToString();
                        if (status_ == "200")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(SignResponse);
                            try
                            {
                                result_ = Newtonsoft.Json.JsonConvert.DeserializeObject<SignResponse>(responseData_, _settings.Value);
                                return result_;
                            }
                            catch (System.Exception exception_)
                            {
                                throw new IoTEdgedException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                        }
                        else
                        if (status_ == "404")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(ErrorResponse);
                            try
                            {
                                result_ = Newtonsoft.Json.JsonConvert.DeserializeObject<ErrorResponse>(responseData_, _settings.Value);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new IoTEdgedException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                            throw new IoTEdgedException<ErrorResponse>("Not Found", (int)response_.StatusCode, responseData_, headers_, result_, null);
                        }
                        else
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(ErrorResponse);
                            try
                            {
                                result_ = Newtonsoft.Json.JsonConvert.DeserializeObject<ErrorResponse>(responseData_, _settings.Value);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new IoTEdgedException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                            throw new IoTEdgedException<ErrorResponse>("Error", (int)response_.StatusCode, responseData_, headers_, result_, null);
                        }
                    }
                    finally
                    {
                        if (response_ != null)
                            response_.Dispose();
                    }
                }
            }
            finally
            {
            }
        }



        public System.Threading.Tasks.Task<TrustBundleResponse> TrustBundleAsync(string api_version)
        {
            return TrustBundleAsync(api_version, System.Threading.CancellationToken.None);
        }


        public async System.Threading.Tasks.Task<TrustBundleResponse> TrustBundleAsync(string api_version, System.Threading.CancellationToken cancellationToken)
        {
            if (api_version == null)
                throw new System.ArgumentNullException("api_version");

            var urlBuilder_ = new System.Text.StringBuilder();
            urlBuilder_.Append(BaseUrl != null ? BaseUrl.TrimEnd('/') : "").Append("/trust-bundle?");
            urlBuilder_.Append("api-version=").Append(System.Net.WebUtility.UrlEncode(ConvertToString(api_version, System.Globalization.CultureInfo.InvariantCulture))).Append("&");
            urlBuilder_.Length--;

            var client_ = _httpClient;
            try
            {
                using (var request_ = new System.Net.Http.HttpRequestMessage())
                {
                    request_.Method = new System.Net.Http.HttpMethod("GET");
                    request_.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var url_ = urlBuilder_.ToString();
                    request_.RequestUri = new System.Uri(url_, System.UriKind.RelativeOrAbsolute);

                    var response_ = await client_.SendAsync(request_, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var headers_ = System.Linq.Enumerable.ToDictionary(response_.Headers, h_ => h_.Key, h_ => h_.Value);
                        if (response_.Content != null && response_.Content.Headers != null)
                        {
                            foreach (var item_ in response_.Content.Headers)
                                headers_[item_.Key] = item_.Value;
                        }

                        var status_ = ((int)response_.StatusCode).ToString();
                        if (status_ == "200")
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(TrustBundleResponse);
                            try
                            {
                                result_ = Newtonsoft.Json.JsonConvert.DeserializeObject<TrustBundleResponse>(responseData_, _settings.Value);
                                return result_;
                            }
                            catch (System.Exception exception_)
                            {
                                throw new IoTEdgedException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                        }
                        else
                        {
                            var responseData_ = response_.Content == null ? null : await response_.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var result_ = default(ErrorResponse);
                            try
                            {
                                result_ = Newtonsoft.Json.JsonConvert.DeserializeObject<ErrorResponse>(responseData_, _settings.Value);
                            }
                            catch (System.Exception exception_)
                            {
                                throw new IoTEdgedException("Could not deserialize the response body.", (int)response_.StatusCode, responseData_, headers_, exception_);
                            }
                            throw new IoTEdgedException<ErrorResponse>("Error", (int)response_.StatusCode, responseData_, headers_, result_, null);
                        }
                    }
                    finally
                    {
                        if (response_ != null)
                            response_.Dispose();
                    }
                }
            }
            finally
            {
            }
        }

        private string ConvertToString(object value, System.Globalization.CultureInfo cultureInfo)
        {
            if (value is System.Enum)
            {
                string name = System.Enum.GetName(value.GetType(), value);
                if (name != null)
                {
                    var field = System.Reflection.IntrospectionExtensions.GetTypeInfo(value.GetType()).GetDeclaredField(name);
                    if (field != null)
                    {
                        var attribute = System.Reflection.CustomAttributeExtensions.GetCustomAttribute(field, typeof(System.Runtime.Serialization.EnumMemberAttribute))
                            as System.Runtime.Serialization.EnumMemberAttribute;
                        if (attribute != null)
                        {
                            return attribute.Value;
                        }
                    }
                }
            }
            else if (value is byte[])
            {
                return System.Convert.ToBase64String((byte[])value);
            }
            else if (value.GetType().IsArray)
            {
                var array = System.Linq.Enumerable.OfType<object>((System.Array)value);
                return string.Join(",", System.Linq.Enumerable.Select(array, o => ConvertToString(o, cultureInfo)));
            }

            return System.Convert.ToString(value, cultureInfo);
        }
    }

    public partial class SignRequest : System.ComponentModel.INotifyPropertyChanged
    {
        private string _keyId;
        private SignRequestAlgo _algo;
        private byte[] _data;

        /// <summary>Name of key to perform sign operation.</summary>
        [Newtonsoft.Json.JsonProperty("keyId", Required = Newtonsoft.Json.Required.Always)]
        public string KeyId
        {
            get { return _keyId; }
            set
            {
                if (_keyId != value)
                {
                    _keyId = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>Sign algorithm to be used.</summary>
        [Newtonsoft.Json.JsonProperty("algo", Required = Newtonsoft.Json.Required.Always)]
        [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
        public SignRequestAlgo Algo
        {
            get { return _algo; }
            set
            {
                if (_algo != value)
                {
                    _algo = value;
                    RaisePropertyChanged();
                }
            }
        }

        /// <summary>Data to be signed.</summary>
        [Newtonsoft.Json.JsonProperty("data", Required = Newtonsoft.Json.Required.Always)]
        public byte[] Data
        {
            get { return _data; }
            set
            {
                if (_data != value)
                {
                    _data = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static SignRequest FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<SignRequest>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.4.0 (Newtonsoft.Json v12.0.0.0)")]
    public partial class SignResponse : System.ComponentModel.INotifyPropertyChanged
    {
        private byte[] _digest;

        /// <summary>Signature of the data.</summary>
        [Newtonsoft.Json.JsonProperty("digest", Required = Newtonsoft.Json.Required.Always)]
        public byte[] Digest
        {
            get { return _digest; }
            set
            {
                if (_digest != value)
                {
                    _digest = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static SignResponse FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<SignResponse>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }



    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.4.0 (Newtonsoft.Json v12.0.0.0)")]
    public partial class TrustBundleResponse : System.ComponentModel.INotifyPropertyChanged
    {
        private string _certificate;

        /// <summary>Base64 encoded PEM formatted byte array containing the trusted certificates.</summary>
        [Newtonsoft.Json.JsonProperty("certificate", Required = Newtonsoft.Json.Required.Always)]
        public string Certificate
        {
            get { return _certificate; }
            set
            {
                if (_certificate != value)
                {
                    _certificate = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static TrustBundleResponse FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<TrustBundleResponse>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }


    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.4.0 (Newtonsoft.Json v12.0.0.0)")]
    public partial class ErrorResponse : System.ComponentModel.INotifyPropertyChanged
    {
        private string _message;

        [Newtonsoft.Json.JsonProperty("message", Required = Newtonsoft.Json.Required.Always)]
        public string Message
        {
            get { return _message; }
            set
            {
                if (_message != value)
                {
                    _message = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string ToJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }

        public static ErrorResponse FromJson(string data)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<ErrorResponse>(data);
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.3.4.0 (Newtonsoft.Json v12.0.0.0)")]
    public enum SignRequestAlgo
    {
        [System.Runtime.Serialization.EnumMember(Value = "HMACSHA256")]
        HMACSHA256 = 0,

    }

    [System.CodeDom.Compiler.GeneratedCode("NSwag", "13.10.2.0 (NJsonSchema v10.3.4.0 (Newtonsoft.Json v12.0.0.0))")]
    public partial class IoTEdgedException : System.Exception
    {
        public int StatusCode { get; private set; }

        public string Response { get; private set; }

        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>> Headers { get; private set; }

        public IoTEdgedException(string message, int statusCode, string response, System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>> headers, System.Exception innerException)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            Response = response;
            Headers = headers;
        }

        public override string ToString()
        {
            return string.Format("HTTP Response: \n\n{0}\n\n{1}", Response, base.ToString());
        }
    }

    [System.CodeDom.Compiler.GeneratedCode("NSwag", "13.10.2.0 (NJsonSchema v10.3.4.0 (Newtonsoft.Json v12.0.0.0))")]
    public partial class IoTEdgedException<TResult> : IoTEdgedException
    {
        public TResult Result { get; private set; }

        public IoTEdgedException(string message, int statusCode, string response, System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>> headers, TResult result, System.Exception innerException)
            : base(message, statusCode, response, headers, innerException)
        {
            Result = result;
        }
    }
}

