// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class DeviceScopeApiClient : IDeviceScopeApiClient
    {
        const int RetryCount = 2;
        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();
        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(RetryCount, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));

        const string InScopeIdentitiesUriTemplate = "/devices/{0}/modules/{1}/devicesAndModulesInDeviceScope?deviceCount={2}&continuationToken={3}&api-version={4}";
        const string InScopeTargetIdentityUriFormat = "/devices/{0}/modules/{1}/deviceAndModuleInDeviceScope?targetDeviceId={2}&targetModuleId={3}&api-version={4}";

        readonly RetryStrategy retryStrategy;
        readonly Uri iotHubBaseHttpUri;
        readonly string deviceId;
        readonly string moduleId;
        readonly int batchSize;
        readonly ITokenProvider edgeHubTokenProvider;
        const string ApiVersion = "2018-08-30-preview";

        public DeviceScopeApiClient(
            string iotHubHostName,
            string deviceId,
            string moduleId,
            int batchSize,
            ITokenProvider edgeHubTokenProvider,
            RetryStrategy retryStrategy = null)
        {
            Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.iotHubBaseHttpUri = new UriBuilder(Uri.UriSchemeHttps, iotHubHostName).Uri;
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.batchSize = Preconditions.CheckRange(batchSize, 0, 1000, nameof(batchSize));
            this.edgeHubTokenProvider = Preconditions.CheckNotNull(edgeHubTokenProvider, nameof(edgeHubTokenProvider));
            this.retryStrategy = retryStrategy ?? TransientRetryStrategy;
        }

        public Task<ScopeResult> GetIdentitiesInScope() =>
            this.GetIdentitiesInScopeWithRetry(this.GetServiceUri(Option.None<string>()));

        public Task<ScopeResult> GetNext(string continuationToken) =>
            this.GetIdentitiesInScopeWithRetry(this.GetServiceUri(Option.Some(continuationToken)));

        public Task<ScopeResult> GetIdentity(string targetDeviceId, string targetModuleId)
        {
            Preconditions.CheckNonWhiteSpace(targetDeviceId, nameof(targetDeviceId));            
            return this.GetIdentitiesInScopeWithRetry(this.GetServiceUri(targetDeviceId, targetModuleId));
        }

        internal Uri GetServiceUri(Option<string> continuationToken) =>
            continuationToken
                .Map(c => new Uri(this.iotHubBaseHttpUri, c))
                .GetOrElse(
                    () =>
                    {
                        string relativeUri = InScopeIdentitiesUriTemplate.FormatInvariant(this.deviceId, this.moduleId, this.batchSize, null, ApiVersion);
                        var uri = new Uri(this.iotHubBaseHttpUri, relativeUri);
                        return uri;
                    });

        internal Uri GetServiceUri(string targetDeviceId, string targetModuleId)
        {
            string relativeUri = InScopeTargetIdentityUriFormat.FormatInvariant(this.deviceId, this.moduleId, targetDeviceId, targetModuleId, ApiVersion);
            var uri = new Uri(this.iotHubBaseHttpUri, relativeUri);
            return uri;
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
            var client = new HttpClient();
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

        static Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, Action<RetryingEventArgs> onRetry, RetryStrategy retryStrategy)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, retryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            static readonly ISet<Type> NonTransientExceptions = new HashSet<Type>
            {
                typeof(ArgumentException),
                typeof(UnauthorizedException)
            };

            public bool IsTransient(Exception ex) => !(NonTransientExceptions.Contains(ex.GetType()));
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<DeviceScopeApiClient>();
            const int IdStart = CloudProxyEventIds.DeviceScopeApiClient;

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
