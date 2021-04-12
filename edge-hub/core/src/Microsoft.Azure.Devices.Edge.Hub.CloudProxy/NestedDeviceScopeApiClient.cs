// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class NestedDeviceScopeApiClient : IDeviceScopeApiClient
    {
        const int RetryCount = 2;

        const string GetDevicesAndModulesInTargetScopeUriFormat = "/devices/{0}/modules/{1}/devicesAndModulesInTargetDeviceScope?api-version={2}";
        const string GetDeviceAndModuleOnBehalfOfUriFormat = "/devices/{0}/modules/{1}/getDeviceAndModuleOnBehalfOf?api-version={2}";

        const string NestedApiVersion = "2020-06-30-preview";

        static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(2);
        // Timeout set to HttpClient is > OperationTimeout because HttpClient throws TaskCanceledException when it timeouts and retry policy won't retry it in that case
        static readonly TimeSpan HttpOperationTimeout = TimeSpan.FromMinutes(5);

        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(RetryCount, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(4));

        readonly RetryStrategy retryStrategy;

        readonly Uri iotHubBaseHttpUri;
        readonly string actorEdgeDeviceId;
        readonly string moduleId;
        readonly Option<string> continuationToken;
        readonly int batchSize;
        readonly ITokenProvider edgeHubTokenProvider;
        readonly IServiceIdentityHierarchy serviceIdentityHierarchy;
        readonly Option<IWebProxy> proxy;

        public string TargetEdgeDeviceId { get; }

        public NestedDeviceScopeApiClient(
            string iotHubHostName,
            string deviceId,
            string moduleId,
            Option<string> continuationToken,
            int batchSize,
            ITokenProvider edgeHubTokenProvider,
            IServiceIdentityHierarchy serviceIdentityHierarchy,
            Option<IWebProxy> proxy,
            RetryStrategy retryStrategy = null)
            : this(iotHubHostName, deviceId, deviceId, moduleId, continuationToken, batchSize, edgeHubTokenProvider, serviceIdentityHierarchy, proxy, retryStrategy)
        {
        }

        public NestedDeviceScopeApiClient(
            string iotHubHostName,
            string deviceId,
            string targetDeviceId,
            string moduleId,
            Option<string> continuationToken,
            int batchSize,
            ITokenProvider edgeHubTokenProvider,
            IServiceIdentityHierarchy serviceIdentityHierarchy,
            Option<IWebProxy> proxy,
            RetryStrategy retryStrategy = null)
        {
            Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.iotHubBaseHttpUri = new UriBuilder(Uri.UriSchemeHttps, iotHubHostName).Uri;
            this.actorEdgeDeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.TargetEdgeDeviceId = Preconditions.CheckNonWhiteSpace(targetDeviceId, nameof(targetDeviceId));
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.continuationToken = Preconditions.CheckNotNull(continuationToken);
            this.batchSize = Preconditions.CheckRange(batchSize, 0, 1000, nameof(batchSize));
            this.edgeHubTokenProvider = Preconditions.CheckNotNull(edgeHubTokenProvider, nameof(edgeHubTokenProvider));
            this.serviceIdentityHierarchy = Preconditions.CheckNotNull(serviceIdentityHierarchy, nameof(serviceIdentityHierarchy));
            this.proxy = Preconditions.CheckNotNull(proxy, nameof(proxy));
            this.retryStrategy = retryStrategy ?? TransientRetryStrategy;
        }

        public Task<ScopeResult> GetIdentitiesInScopeAsync() =>
            this.GetIdentitiesInTargetScopeWithRetry(this.GetNestedScopeServiceUri(), Option.None<string>());

        public Task<ScopeResult> GetNextAsync(string continuationToken) =>
            this.GetIdentitiesInTargetScopeWithRetry(this.GetNestedScopeServiceUri(), Option.Some(continuationToken));

        public Task<ScopeResult> GetIdentityAsync(string deviceId, string moduleId) => throw new NotImplementedException("Use GetIdentityOnBehalfOfAsync() instead");

        public Task<ScopeResult> GetIdentityOnBehalfOfAsync(string targetDeviceId, Option<string> targetModuleId, string onBehalfOfDevice)
        {
            Preconditions.CheckNonWhiteSpace(targetDeviceId, nameof(targetDeviceId));
            Preconditions.CheckNonWhiteSpace(onBehalfOfDevice, nameof(onBehalfOfDevice));
            return this.GetIdentityOnBehalfOfWithRetry(this.GetIdentityOnBehalfOfServiceUri(), targetDeviceId, targetModuleId, onBehalfOfDevice);
        }

        internal Uri GetNestedScopeServiceUri()
        {
            // The URI is always in the context of the actor device
            string relativeUri = GetDevicesAndModulesInTargetScopeUriFormat.FormatInvariant(this.actorEdgeDeviceId, this.moduleId, NestedApiVersion);
            var uri = new Uri(this.iotHubBaseHttpUri, relativeUri);
            return uri;
        }

        internal Uri GetIdentityOnBehalfOfServiceUri()
        {
            // The URI is always in the context of the actor device
            string relativeUri = GetDeviceAndModuleOnBehalfOfUriFormat.FormatInvariant(this.actorEdgeDeviceId, this.moduleId, NestedApiVersion);
            var uri = new Uri(this.iotHubBaseHttpUri, relativeUri);
            return uri;
        }

        static Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, Action<RetryingEventArgs> onRetry, RetryStrategy retryStrategy)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, retryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        async Task<ScopeResult> GetIdentityOnBehalfOfWithRetry(Uri uri, string deviceId, Option<string> moduleId, string onBehalfOfDevice)
        {
            try
            {
                return await ExecuteWithRetry(
                    () => this.GetIdentityOnBehalfOfInternalAsync(uri, deviceId, moduleId, onBehalfOfDevice).TimeoutAfter(OperationTimeout),
                    Events.RetryingGetIdentities,
                    this.retryStrategy);
            }
            catch (Exception e)
            {
                Events.ErrorInScopeResult(e);
                throw;
            }
        }

        async Task<ScopeResult> GetIdentitiesInTargetScopeWithRetry(Uri uri, Option<string> continuationToken)
        {
            // If the caller supplied a continuation token, then it overrides the cached token
            Option<string> continuation = continuationToken.HasValue ? continuationToken : this.continuationToken;

            try
            {
                return await ExecuteWithRetry(
                    () => this.GetIdentitiesInTargetScopeInternalAsync(uri, continuation).TimeoutAfter(OperationTimeout),
                    Events.RetryingGetIdentities,
                    this.retryStrategy);
            }
            catch (Exception e)
            {
                Events.ErrorInScopeResult(e);
                throw;
            }
        }

        async Task<ScopeResult> GetIdentityOnBehalfOfInternalAsync(Uri uri, string deviceId, Option<string> moduleId, string onBehalfOfDevice)
        {
            HttpClient client = this.proxy
                .Map(p => new HttpClient(new HttpClientHandler { Proxy = p }, disposeHandler: true))
                .GetOrElse(() => new HttpClient());
            client.Timeout = HttpOperationTimeout;
            using (var msg = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                // Get the auth-chain for the Edge we're acting OnBehalfOf
                Option<string> maybeAuthChain = await this.serviceIdentityHierarchy.GetAuthChain(onBehalfOfDevice);
                string authChain = maybeAuthChain.Expect(() => new InvalidOperationException($"No valid authentication chain for {onBehalfOfDevice}"));

                var payload = new IdentityOnBehalfOfRequest(deviceId, moduleId.OrDefault(), authChain);
                string token = await this.edgeHubTokenProvider.GetTokenAsync(Option.None<TimeSpan>());
                msg.Headers.Add(HttpRequestHeader.Authorization.ToString(), token);
                msg.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

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
                    throw new DeviceScopeApiException("Error getting device scope result from upstream", response.StatusCode, content);
                }
            }
        }

        async Task<ScopeResult> GetIdentitiesInTargetScopeInternalAsync(Uri uri, Option<string> continuationToken)
        {
            HttpClient client = this.proxy
                .Map(p => new HttpClient(new HttpClientHandler { Proxy = p }, disposeHandler: true))
                .GetOrElse(() => new HttpClient());
            client.Timeout = HttpOperationTimeout;
            using (var msg = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                // Get the auth-chain for the target device
                Option<string> maybeAuthChain = await this.serviceIdentityHierarchy.GetAuthChain(this.TargetEdgeDeviceId);
                string authChain = maybeAuthChain.Expect(() => new InvalidOperationException($"No valid authentication chain for {this.TargetEdgeDeviceId}"));

                var payload = new NestedScopeRequest(this.batchSize, continuationToken.OrDefault(), authChain);
                string token = await this.edgeHubTokenProvider.GetTokenAsync(Option.None<TimeSpan>());
                msg.Headers.Add(HttpRequestHeader.Authorization.ToString(), token);
                msg.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

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
                    throw new DeviceScopeApiException("Error getting device scope result from upstream", response.StatusCode, content);
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
            const int IdStart = CloudProxyEventIds.NestedDeviceScopeApiClient;
            static readonly ILogger Log = Logger.Factory.CreateLogger<NestedDeviceScopeApiClient>();

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
