// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Edged.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using ErrorResponse = Microsoft.Azure.Devices.Edge.Util.Edged.GeneratedCode.ErrorResponse;
    using SignRequest = Microsoft.Azure.Devices.Edge.Util.Edged.GeneratedCode.SignRequest;
    using SignRequestAlgo = Microsoft.Azure.Devices.Edge.Util.Edged.GeneratedCode.SignRequestAlgo;
    using SignResponse = Microsoft.Azure.Devices.Edge.Util.Edged.GeneratedCode.SignResponse;

    public class HttpHsmSignatureProvider : ISignatureProvider
    {
        const SignRequestAlgo DefaultSignRequestAlgo = SignRequestAlgo.HMACSHA256;
        const string DefaultKeyId = "primary";
        readonly string apiVersion;
        readonly Uri providerUri;
        readonly string moduleId;
        readonly string generationId;

        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();
        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(retryCount: 3, minBackoff: TimeSpan.FromSeconds(2), maxBackoff: TimeSpan.FromSeconds(30), deltaBackoff: TimeSpan.FromSeconds(3));

        public HttpHsmSignatureProvider(string moduleId, string generationId, string providerUri, string apiVersion)
        {            
            this.providerUri = new Uri(Preconditions.CheckNotNull(providerUri, nameof(providerUri)));
            this.apiVersion = Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.generationId = Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
        }

        public async Task<string> SignAsync(string data)
        {
            var signRequest = new SignRequest
            {
                KeyId = DefaultKeyId,
                Algo = DefaultSignRequestAlgo,
                Data = Encoding.UTF8.GetBytes(data)
            };

            HttpClient httpClient = HttpClientHelper.GetHttpClient(this.providerUri);
            try
            {
                var hsmHttpClient = new HttpWorkloadClient(httpClient)
                {
                    BaseUrl = HttpClientHelper.GetBaseUrl(this.providerUri)
                };

                SignResponse response = await this.SignAsyncWithRetry(hsmHttpClient, signRequest);

                return Convert.ToBase64String(response.Digest);
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case IoTEdgedException<ErrorResponse> errorResponseException:
                        throw new HttpHsmCommunicationException(
                            $"Error calling SignAsync: {errorResponseException.Result?.Message ?? string.Empty}",
                            errorResponseException.StatusCode);
                    case IoTEdgedException ioTEdgedException:
                        throw new HttpHsmCommunicationException(
                            $"Error calling SignAsync: {ioTEdgedException.Response ?? string.Empty}",
                            ioTEdgedException.StatusCode);
                    default:
                        throw;
                }
            }
            finally
            {
                httpClient.Dispose();
            }
        }

        async Task<SignResponse> SignAsyncWithRetry(HttpWorkloadClient hsmHttpClient, SignRequest signRequest)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            SignResponse response = await transientRetryPolicy.ExecuteAsync(() => hsmHttpClient.SignAsync(this.apiVersion, this.moduleId, this.generationId, signRequest));
            return response;
        }

        class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex) => ex is IoTEdgedException se && se.StatusCode >= 500;
        }
    }
}
