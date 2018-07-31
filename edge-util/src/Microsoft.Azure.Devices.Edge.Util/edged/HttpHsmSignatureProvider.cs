// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client.HsmAuthentication.GeneratedCode;
using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;

namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
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
            new TransientFaultHandling.ExponentialBackoff(retryCount: 3, minBackoff: TimeSpan.FromSeconds(2), maxBackoff: TimeSpan.FromSeconds(30), deltaBackoff: TimeSpan.FromSeconds(3));

        public HttpHsmSignatureProvider(string moduleId, string generationId, string providerUri, string apiVersion)
        {
            if (string.IsNullOrEmpty(providerUri))
            {
                throw new ArgumentNullException(nameof(providerUri));
            }
            if (string.IsNullOrEmpty(apiVersion))
            {
                throw new ArgumentNullException(nameof(apiVersion));
            }

            this.providerUri = new Uri(providerUri);
            this.apiVersion = apiVersion;
            this.moduleId = moduleId;
            this.generationId = generationId;
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
                var hsmHttpClient = new HttpHsmClient(httpClient)
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
                    case SwaggerException<ErrorResponse> errorResponseException:
                        throw new HttpHsmComunicationException(
                            $"Error calling SignAsync: {errorResponseException.Result?.Message ?? string.Empty}",
                            errorResponseException.StatusCode);
                    case SwaggerException swaggerException:
                        throw new HttpHsmComunicationException(
                            $"Error calling SignAsync: {swaggerException.Response ?? string.Empty}",
                            swaggerException.StatusCode);
                    default:
                        throw;
                }
            }
            finally
            {
                httpClient.Dispose();
            }
        }

        async Task<SignResponse> SignAsyncWithRetry(HttpHsmClient hsmHttpClient, SignRequest signRequest)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            SignResponse response = await transientRetryPolicy.ExecuteAsync(() => hsmHttpClient.SignAsync(this.apiVersion, this.moduleId, this.generationId, signRequest));
            return response;
        }

        class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex) => ex is SwaggerException se && se.StatusCode >= 500;
        }
    }
}
