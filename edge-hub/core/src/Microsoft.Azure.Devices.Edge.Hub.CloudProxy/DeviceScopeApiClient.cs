// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class DeviceScopeApiClient : IDeviceScopeApiClient
    {
        const int RetryCount = 2;

        const string InScopeIdentitiesUriTemplate = "/devices/{0}/modules/{1}/devicesAndModulesInDeviceScope?deviceCount={2}&continuationToken={3}&api-version={4}";

        const string InScopeTargetIdentityUriFormat = "/devices/{0}/modules/{1}/deviceAndModuleInDeviceScope?targetDeviceId={2}&targetModuleId={3}&api-version={4}";

        const string ApiVersion = "2018-08-30-preview";

        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(RetryCount, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(4));

        readonly RetryStrategy retryStrategy;

        readonly Uri iotHubBaseHttpUri;
        readonly string moduleId;
        readonly int batchSize;
        readonly ITokenProvider edgeHubTokenProvider;
        readonly Option<IWebProxy> proxy;

        public string TargetEdgeDeviceId { get; }

        public DeviceScopeApiClient(
            string iotHubHostName,
            string deviceId,
            string moduleId,
            int batchSize,
            ITokenProvider edgeHubTokenProvider,
            Option<IWebProxy> proxy,
            RetryStrategy retryStrategy = null)
        {
            Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.iotHubBaseHttpUri = new UriBuilder(Uri.UriSchemeHttps, iotHubHostName).Uri;
            this.TargetEdgeDeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.batchSize = Preconditions.CheckRange(batchSize, 0, 1000, nameof(batchSize));
            this.edgeHubTokenProvider = Preconditions.CheckNotNull(edgeHubTokenProvider, nameof(edgeHubTokenProvider));
            this.proxy = Preconditions.CheckNotNull(proxy, nameof(proxy));
            this.retryStrategy = retryStrategy ?? TransientRetryStrategy;
        }

        public Task<ScopeResult> GetIdentitiesInScopeAsync() =>
            this.GetIdentitiesInScopeWithRetry(this.GetServiceUri(Option.None<string>()));

        public Task<ScopeResult> GetNextAsync(string continuationToken) =>
            this.GetIdentitiesInScopeWithRetry(this.GetServiceUri(Option.Some(continuationToken)));

        public Task<ScopeResult> GetIdentityAsync(string requestedDeviceId, string requestedModuleId)
        {
            Preconditions.CheckNonWhiteSpace(requestedDeviceId, nameof(requestedDeviceId));
            return this.GetIdentitiesInScopeWithRetry(this.GetServiceUri(requestedDeviceId, requestedModuleId));
        }

        internal Uri GetServiceUri(Option<string> continuationToken) =>
            continuationToken
                .Map(c => new Uri(this.iotHubBaseHttpUri, c))
                .GetOrElse(
                    () =>
                    {
                        string relativeUri = InScopeIdentitiesUriTemplate.FormatInvariant(this.TargetEdgeDeviceId, this.moduleId, this.batchSize, null, ApiVersion);
                        var uri = new Uri(this.iotHubBaseHttpUri, relativeUri);
                        return uri;
                    });

        internal Uri GetServiceUri(string requestedDeviceId, string requestedModuleId)
        {
            string relativeUri = InScopeTargetIdentityUriFormat.FormatInvariant(this.TargetEdgeDeviceId, this.moduleId, requestedDeviceId, requestedModuleId, ApiVersion);
            var uri = new Uri(this.iotHubBaseHttpUri, relativeUri);
            return uri;
        }

        static Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, Action<RetryingEventArgs> onRetry, RetryStrategy retryStrategy)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, retryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        async Task<ScopeResult> GetIdentitiesInScopeWithRetry(Uri uri)
        {
            try
            {
                return await ExecuteWithRetry(
                    () => this.GetIdentitiesInScope(uri),
                    Events.RetryingGetIdentities,
                    this.retryStrategy);
            }
            catch (Exception e)
            {
                Events.ErrorInScopeResult(e);
                throw;
            }
        }

        async Task<ScopeResult> GetIdentitiesInScope(Uri uri)
        {
            HttpClient client = this.proxy
                .Map(p => new HttpClient(new HttpClientHandler { Proxy = p }, disposeHandler: true))
                .GetOrElse(() => new HttpClient());
            using (var msg = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                string token = await this.edgeHubTokenProvider.GetTokenAsync(Option.None<TimeSpan>());
                msg.Headers.Add(HttpRequestHeader.Authorization.ToString(), token);

                HttpResponseMessage response = await client.SendAsync(msg);
                string content = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    var scopeResult = JsonConvert.DeserializeObject<ScopeResult>(content);
                    Events.GotValidResult();
                    return scopeResult;
                }
                else
                {
                    throw new DeviceScopeApiException("Error getting device scope result from IoTHub", response.StatusCode, content);
                }
            }
        }

        internal class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            static readonly ISet<Type> NonTransientExceptions = new HashSet<Type>
            {
                typeof(ArgumentException),
                typeof(ArgumentNullException),
                typeof(InvalidOperationException)
            };

            static readonly ISet<HttpStatusCode> NonTransientHttpStatusCodes = new HashSet<HttpStatusCode>
            {
                HttpStatusCode.BadRequest,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Forbidden,
                HttpStatusCode.NotFound,
                HttpStatusCode.MethodNotAllowed,
                HttpStatusCode.NotAcceptable
            };

            public bool IsTransient(Exception ex)
            {
                // Treat all responses with 4xx HttpStatusCode as non-transient
                if (ex is DeviceScopeApiException deviceScopeApiException
                    && NonTransientHttpStatusCodes.Contains(deviceScopeApiException.StatusCode))
                {
                    return false;
                }
                else if (NonTransientExceptions.Contains(ex.GetType()))
                {
                    return false;
                }

                return true;
            }
        }

        static class Events
        {
            const int IdStart = CloudProxyEventIds.DeviceScopeApiClient;
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceScopeApiClient>();

            enum EventIds
            {
                Retrying = IdStart,
                ScopeResultReceived,
                ErrorInScopeResult
            }

            public static void RetryingGetIdentities(RetryingEventArgs retryingEventArgs)
            {
                Log.LogDebug((int)EventIds.Retrying, $"Retrying device scope api call {retryingEventArgs.CurrentRetryCount} times because of error - {retryingEventArgs.LastException}");
            }

            public static void GotValidResult()
            {
                Log.LogDebug((int)EventIds.ScopeResultReceived, "Got valid device scope result");
            }

            public static void ErrorInScopeResult(Exception ex)
            {
                Log.LogDebug((int)EventIds.ErrorInScopeResult, ex, "Error getting device scope result from the cloud");
            }
        }
    }
}
