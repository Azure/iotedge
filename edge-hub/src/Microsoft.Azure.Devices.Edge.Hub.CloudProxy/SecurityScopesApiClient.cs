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
    using Newtonsoft.Json;

    public interface ISecurityScopesApiClient
    {
        Task<ScopeResult> GetIdentitiesInScope();

        Task<ScopeResult> GetIdentitiesInScope(Uri uri);

        Task<ScopeResult> GetIdentity(string deviceId, string moduleId);
    }

    public class SecurityScopesApiClient : ISecurityScopesApiClient
    {        
        const string InScopeIdentitiesUriTemplate = "/devices/{0}/modules/{1}/devicesAndModulesInSecurityScope?deviceCount={2}&continuationToken={3}&api-version={4}";
        const string InScopeTargetIdentityUriFormat = "/devices/{0}/modules/{1}/deviceAndModuleInSecurityScope?targetDeviceId={2}&targetModuleId={3}&api-version={4}";

        readonly Uri iotHubBaseHttpUri;
        readonly string deviceId;
        readonly string moduleId;
        readonly int batchSize;

        public SecurityScopesApiClient(string iotHubHostName, string deviceId, string moduleId, int batchSize)
        {
            Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            this.iotHubBaseHttpUri = new UriBuilder(Uri.UriSchemeHttps, iotHubHostName).Uri;
            this.deviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.moduleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.batchSize = Preconditions.CheckRange(batchSize, 0, 1000, nameof(batchSize));
        }

        public Task<ScopeResult> GetIdentitiesInScope()
        {
            string relativeUri = InScopeIdentitiesUriTemplate.FormatInvariant(this.deviceId, this.moduleId, this.batchSize, null);
            var uri = new Uri(this.iotHubBaseHttpUri, relativeUri);
            return this.GetIdentitiesInScope(uri);
        }

        public async Task<ScopeResult> GetIdentitiesInScope(Uri uri)
        {
            var client = new HttpClient();
            using (var msg = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                //string token = await ehAuth.GetTokenAsync(IoTHub);
                //msg.Headers.Add(HttpRequestHeader.Authorization.ToString(),
                //    token);
                //var moduleInfo =
                //    @"{""moduleId"":""$edgeHub"",""deviceId"":""EdgeDevice301"",""generationId"":""636692486564076830"",""etag"":""NDk0MTA0NTIw"",""connectionState"":""Disconnected"",""connectionStateUpdatedTime"":""2018-08-07T14:48:10.2233672"",""lastActivityTime"":""0001-01-01T00:00:00"",""cloudToDeviceMessageCount"":0,""authentication"":{""symmetricKey"":{""primaryKey"":""3cJRkerFdRtLEuS+IIxZW5yz1IeprJ99cr2Om9RiTD0="",""secondaryKey"":""dz6vHy8flvODo8wHL7LK4Q59ATPLCtR89kGDKWa14GA=""},""x509Thumbprint"":{""primaryThumbprint"":null,""secondaryThumbprint"":null},""type"":""sas""},""managedBy"":""iotEdge""}";
                //msg.Headers.Add("iothub-devicemetadata", moduleInfo);

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

        public Task<ScopeResult> GetIdentity(string targetDeviceId, string targetModuleId)
        {
            Preconditions.CheckNonWhiteSpace(targetDeviceId, nameof(targetDeviceId));
            string relativeUri = InScopeTargetIdentityUriFormat.FormatInvariant(this.deviceId, this.moduleId, targetDeviceId, targetModuleId);
            var uri = new Uri(this.iotHubBaseHttpUri, relativeUri);
            return this.GetIdentitiesInScope(uri);
        }
    }
}
