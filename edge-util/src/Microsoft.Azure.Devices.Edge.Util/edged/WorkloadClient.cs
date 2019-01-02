// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Edged.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    public class WorkloadClient
    {
        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();
        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(retryCount: 3, minBackoff: TimeSpan.FromSeconds(2), maxBackoff: TimeSpan.FromSeconds(30), deltaBackoff: TimeSpan.FromSeconds(3));
        readonly Uri workloadUri;
        readonly string apiVersion;
        readonly string moduleId;
        readonly string moduleGenerationId;

        public WorkloadClient(Uri serverUri, string apiVersion, string moduleId, string moduleGenerationId)
        {
            this.workloadUri = Preconditions.CheckNotNull(serverUri, nameof(serverUri));
            this.apiVersion = Preconditions.CheckNonWhiteSpace(apiVersion, nameof(apiVersion));
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.moduleGenerationId = Preconditions.CheckNonWhiteSpace(moduleGenerationId, nameof(moduleGenerationId));
        }

        public async Task<CertificateResponse> CreateServerCertificateAsync(string hostname, DateTime expiration)
        {
            var request = new ServerCertificateRequest
            {
                CommonName = hostname,
                Expiration = expiration
            };

            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.workloadUri))
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.workloadUri) };
                CertificateResponse result = await this.Execute(() => edgeletHttpClient.CreateServerCertificateAsync(this.apiVersion, this.moduleId, this.moduleGenerationId, request), "CreateServerCertificateAsync");
                return result;
            }
        }

        public async Task<TrustBundleResponse> GetTrustBundleAsync()
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.workloadUri))
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.workloadUri) };
                TrustBundleResponse result = await this.Execute(() => edgeletHttpClient.TrustBundleAsync(this.apiVersion), "TrustBundleAsync");
                return result;
            }
        }

        public async Task<string> EncryptAsync(string initializationVector, string plainText)
        {
            var request = new EncryptRequest
            {
                Plaintext = Encoding.UTF8.GetBytes(plainText),
                InitializationVector = Encoding.UTF8.GetBytes(initializationVector)
            };
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.workloadUri))
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.workloadUri) };
                EncryptResponse result = await this.Execute(() => edgeletHttpClient.EncryptAsync(this.apiVersion, this.moduleId, this.moduleGenerationId, request), "Encrypt");
                return Convert.ToBase64String(result.Ciphertext);
            }
        }

        public async Task<string> DecryptAsync(string initializationVector, string encryptedText)
        {
            var request = new DecryptRequest
            {
                Ciphertext = Convert.FromBase64String(encryptedText),
                InitializationVector = Encoding.UTF8.GetBytes(initializationVector)
            };
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.workloadUri))
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.workloadUri) };
                DecryptResponse result = await this.Execute(() => edgeletHttpClient.DecryptAsync(this.apiVersion, this.moduleId, this.moduleGenerationId, request), "Decrypt");
                return Encoding.UTF8.GetString(result.Plaintext);
            }
        }

        async Task<T> Execute<T>(Func<Task<T>> func, string operation)
        {
            try
            {
                Events.ExecutingOperation(operation, this.workloadUri.ToString());
                T result = await ExecuteWithRetry(func, r => Events.RetryingOperation(operation, this.workloadUri.ToString(), r));
                Events.SuccessfullyExecutedOperation(operation, this.workloadUri.ToString());
                return result;
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case IoTEdgedException<ErrorResponse> errorResponseException:
                        throw new WorkloadCommunicationException($"Error calling {operation}: {errorResponseException.Result?.Message ?? string.Empty}", errorResponseException.StatusCode);

                    case IoTEdgedException swaggerException:
                        if (swaggerException.StatusCode < 400)
                        {
                            Events.SuccessfullyExecutedOperation(operation, this.workloadUri.ToString());
                            return default(T);
                        }
                        else
                        {
                            throw new WorkloadCommunicationException($"Error calling {operation}: {swaggerException.Response ?? string.Empty}", swaggerException.StatusCode);
                        }
                    default:
                        throw;
                }
            }
        }

        static Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, Action<RetryingEventArgs> onRetry)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex) => ex is IoTEdgedException se
                && se.StatusCode >= 500;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<WorkloadClient>();
            const int IdStart = UtilEventsIds.EdgeletWorkloadClient;

            enum EventIds
            {
                ExecutingOperation = IdStart,
                SuccessfullyExecutedOperation,
                RetryingOperation
            }

            internal static void RetryingOperation(string operation, string url, RetryingEventArgs r)
            {
                Log.LogDebug((int)EventIds.RetryingOperation, $"Retrying Http call to {url} to {operation} because of error {r.LastException.Message}, retry count = {r.CurrentRetryCount}");
            }

            internal static void ExecutingOperation(string operation, string url)
            {
                Log.LogDebug((int)EventIds.ExecutingOperation, $"Making a Http call to {url} to {operation}");
            }

            internal static void SuccessfullyExecutedOperation(string operation, string url)
            {
                Log.LogDebug((int)EventIds.SuccessfullyExecutedOperation, $"Received a valid Http response from {url} for {operation}");
            }
        }
    }
}
