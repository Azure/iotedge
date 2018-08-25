// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Newtonsoft.Json;

    public interface ITokenProvider
    {
        Task<string> GetTokenAsync(Option<TimeSpan> ttl);
    }

    public class DeviceScopeApiClient : IDeviceScopeApiClient
    {
        const int RetryCount = 3;
        static readonly ITransientErrorDetectionStrategy TimeoutErrorDetectionStrategy = new DelegateErrorDetectionStrategy(ex => ex.HasTimeoutException());
        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(RetryCount, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));


        const string InScopeIdentitiesUriTemplate = "/devices/{0}/modules/{1}/devicesAndModulesInSecurityScope?deviceCount={2}&continuationToken={3}&api-version={4}";
        const string InScopeTargetIdentityUriFormat = "/devices/{0}/modules/{1}/deviceAndModuleInSecurityScope?targetDeviceId={2}&targetModuleId={3}&api-version={4}";

        readonly Uri iotHubBaseHttpUri;
        readonly string deviceId;
        readonly string moduleId;
        readonly int batchSize;
        readonly ITokenProvider edgeHubTokenProvider;
        const string ApiVersion = "preview";

        public DeviceScopeApiClient(string iotHubHostName, string deviceId, string moduleId, int batchSize, ITokenProvider edgeHubTokenProvider)
        {
            Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.iotHubBaseHttpUri = new UriBuilder(Uri.UriSchemeHttps, iotHubHostName).Uri;
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.batchSize = Preconditions.CheckRange(batchSize, 0, 1000, nameof(batchSize));
            this.edgeHubTokenProvider = Preconditions.CheckNotNull(edgeHubTokenProvider, nameof(edgeHubTokenProvider));
        }

        public Task<ScopeResult> GetIdentitiesInScope() =>
            this.GetIdentitiesInScope(this.GetServiceUri(Option.None<string>()));

        public Task<ScopeResult> GetNext(string continuationToken) =>
            this.GetIdentitiesInScope(this.GetServiceUri(Option.Some(continuationToken)));

        public Task<ScopeResult> GetIdentity(string targetDeviceId, string targetModuleId)
        {
            Preconditions.CheckNonWhiteSpace(targetDeviceId, nameof(targetDeviceId));            
            return this.GetIdentitiesInScope(this.GetServiceUri(targetDeviceId, targetModuleId));
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
                    Console.WriteLine(
                        $"Got result with {scopeResult.Devices.Count()} devices and {scopeResult.Modules.Count()} modules and CT {scopeResult.ContinuationLink}");
                    return scopeResult;
                }
                else
                {
                    throw new Exception($"Error getting Scope result from IoTHub. Status code - {response.StatusCode}, Message - {content}");
                }
            }
        }        
    }
}
