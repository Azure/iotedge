// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ClientWrapper.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    class EdgeletSignatureProvider : ISignatureProvider
    {
        const string DefaultApiVersion = "2018-06-28";
        const SignRequestAlgo DefaultSignRequestAlgo = SignRequestAlgo.HMACSHA256;
        readonly string apiVersion;
        readonly string edgeletUri;
        readonly EdgeletWorkloadHttpClient edgeletWorkloadHttpClient;

        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();
        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(retryCount: 3, minBackoff: TimeSpan.FromSeconds(2), maxBackoff: TimeSpan.FromSeconds(30), deltaBackoff: TimeSpan.FromSeconds(3));

        public EdgeletSignatureProvider(string edgeletUri) : this(edgeletUri, DefaultApiVersion)
        {
        }

        public EdgeletSignatureProvider(string edgeletUri, string apiVersion)
        {
            Preconditions.CheckNonWhiteSpace(edgeletUri, nameof(edgeletUri));
            Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));

            this.edgeletWorkloadHttpClient = new EdgeletWorkloadHttpClient()
            {
                BaseUrl = edgeletUri
            };
            this.apiVersion = apiVersion;
            this.edgeletUri = edgeletUri;
        }

        public async Task<string> SignAsync(string keyName, string data)
        {
            Preconditions.CheckNonWhiteSpace(keyName, nameof(keyName));
            Preconditions.CheckNonWhiteSpace(data, nameof(data));

            var signRequest = new SignRequest()
            {
                KeyId = keyName,
                Algo = DefaultSignRequestAlgo,
                Data = Encoding.UTF8.GetBytes(data)
            };

            try
            {
                Events.ExecuteSignAsync(this.edgeletUri);
                SignResponse response = await this.SignAsyncWithRetry(keyName, signRequest);
                Events.SuccessfullyExecutedSignAsync(this.edgeletUri);

                return Convert.ToBase64String(response.Digest);
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case SwaggerException<ErrorResponse> errorResponseException:
                        throw new EdgeletCommunicationException($"Error calling SignAsync: {errorResponseException.Result?.Message ?? string.Empty}", errorResponseException.StatusCode);
                    case SwaggerException swaggerException:
                        throw new EdgeletCommunicationException($"Error calling SignAsync: {swaggerException.Response ?? string.Empty}", swaggerException.StatusCode);
                    default:
                        throw;
                }
            }
        }

        async Task<SignResponse> SignAsyncWithRetry(string keyName, SignRequest signRequest)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => Events.RetryingSignAsync(this.edgeletUri, args);
            SignResponse response = await transientRetryPolicy.ExecuteAsync(() => this.edgeletWorkloadHttpClient.SignAsync(this.apiVersion, keyName, signRequest));
            return response;
        }

        class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex) => ex is SwaggerException se && se.StatusCode >= 500;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<EdgeletSignatureProvider>();
            // Use an ID not used by other components
            const int IdStart = 6000;

            enum EventIds
            {
                ExecutingOperation = IdStart,
                SuccessfullyExecutedOperation,
                RetryingOperation
            }

            internal static void RetryingSignAsync(string url, RetryingEventArgs r)
            {
                Log.LogDebug((int)EventIds.RetryingOperation, $"Retrying Http call to {url} to SignAsync because of error {r.LastException.Message}, retry count = {r.CurrentRetryCount}");
            }

            internal static void ExecuteSignAsync(string url)
            {
                Log.LogDebug((int)EventIds.ExecutingOperation, $"Making a Http call to {url} to SignAsync");
            }

            internal static void SuccessfullyExecutedSignAsync(string url)
            {
                Log.LogDebug((int)EventIds.SuccessfullyExecutedOperation, $"Received a valid Http response from {url} for SignAsync");
            }
        }
    }
}
