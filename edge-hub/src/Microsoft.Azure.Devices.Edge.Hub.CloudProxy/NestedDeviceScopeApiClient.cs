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

        const string NestedApiVersion = "2020-06-30-preview";

        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(RetryCount, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(4));

        readonly RetryStrategy retryStrategy;

        readonly Uri iotHubBaseHttpUri;
        readonly string actorEdgeDeviceId;
        readonly string moduleId;
        readonly int batchSize;
        readonly ITokenProvider edgeHubTokenProvider;
        readonly IAuthenticationChainProvider authChainProvider;
        readonly Option<IWebProxy> proxy;

        public string TargetEdgeDeviceId { get; }

        public NestedDeviceScopeApiClient(
            string iotHubHostName,
            string deviceId,
            string moduleId,
            int batchSize,
            ITokenProvider edgeHubTokenProvider,
            IAuthenticationChainProvider authChainProvider,
            Option<IWebProxy> proxy,
            RetryStrategy retryStrategy = null)
        {
            Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.iotHubBaseHttpUri = new UriBuilder(Uri.UriSchemeHttps, iotHubHostName).Uri;
            this.actorEdgeDeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.TargetEdgeDeviceId = this.actorEdgeDeviceId;
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.batchSize = Preconditions.CheckRange(batchSize, 0, 1000, nameof(batchSize));
            this.edgeHubTokenProvider = Preconditions.CheckNotNull(edgeHubTokenProvider, nameof(edgeHubTokenProvider));
            this.authChainProvider = Preconditions.CheckNotNull(authChainProvider, nameof(authChainProvider));
            this.proxy = Preconditions.CheckNotNull(proxy, nameof(proxy));
            this.retryStrategy = retryStrategy ?? TransientRetryStrategy;
        }

        public NestedDeviceScopeApiClient(string childDeviceId, NestedDeviceScopeApiClient client)
        {
            this.TargetEdgeDeviceId = Preconditions.CheckNonWhiteSpace(childDeviceId, nameof(childDeviceId));
            this.actorEdgeDeviceId = client.actorEdgeDeviceId;
            this.iotHubBaseHttpUri = client.iotHubBaseHttpUri;
            this.moduleId = client.moduleId;
            this.batchSize = client.batchSize;
            this.edgeHubTokenProvider = client.edgeHubTokenProvider;
            this.proxy = client.proxy;
            this.retryStrategy = client.retryStrategy;
            this.authChainProvider = client.authChainProvider;
        }

        public IDeviceScopeApiClient CreateOnBehalfOfDeviceScopeClient(string deviceId)
        {
            return new NestedDeviceScopeApiClient(deviceId, this);
        }

        public Task<ScopeResult> GetIdentitiesInScope() =>
            this.GetIdentitiesInTargetScopeWithRetry(this.GetServiceUri(), Option.None<string>());

        public Task<ScopeResult> GetNext(string continuationToken) =>
            this.GetIdentitiesInTargetScopeWithRetry(this.GetServiceUri(), Option.Some(continuationToken));

        public Task<ScopeResult> GetIdentity(string _, string __)
        {
            // TODO: 7026875: Refreshing scopes on Connect
            return Task.FromResult(new ScopeResult(Enumerable.Empty<Device>(), Enumerable.Empty<Module>(), string.Empty));
        }

        internal Uri GetServiceUri()
        {
            // The URI is always always in the context of the root device
            string relativeUri = GetDevicesAndModulesInTargetScopeUriFormat.FormatInvariant(this.actorEdgeDeviceId, this.moduleId, NestedApiVersion);
            var uri = new Uri(this.iotHubBaseHttpUri, relativeUri);
            return uri;
        }

        static Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, Action<RetryingEventArgs> onRetry, RetryStrategy retryStrategy)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, retryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        async Task<ScopeResult> GetIdentitiesInTargetScopeWithRetry(Uri uri, Option<string> continuationToken)
        {
            try
            {
                return await ExecuteWithRetry(
                    () => this.GetIdentitiesInTargetScope(uri, continuationToken),
                    Events.RetryingGetIdentities,
                    this.retryStrategy);
            }
            catch (Exception e)
            {
                Events.ErrorInScopeResult(e);
                throw;
            }
        }

        async Task<ScopeResult> GetIdentitiesInTargetScope(Uri uri, Option<string> continuationLink)
        {
            HttpClient client = this.proxy
                .Map(p => new HttpClient(new HttpClientHandler { Proxy = p }, disposeHandler: true))
                .GetOrElse(() => new HttpClient());
            using (var msg = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                // Get the auth-chain for the target device
                if (!this.authChainProvider.TryGetAuthChain(this.TargetEdgeDeviceId, out string authChain))
                {
                    throw new ArgumentException($"No valid authentication chain for {this.TargetEdgeDeviceId}");
                }

                var payload = new NestedScopeRequest()
                {
                    AuthChain = authChain,
                    PageSize = this.batchSize,
                    ContinuationLink = continuationLink.GetOrElse(() => string.Empty)
                };

                string token = await this.edgeHubTokenProvider.GetTokenAsync(Option.None<TimeSpan>());
                msg.Headers.Add(HttpRequestHeader.Authorization.ToString(), token);
                msg.Content = new StringContent(JsonConvert.SerializeObject(payload).ToString(), Encoding.Default, "application/json");

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
