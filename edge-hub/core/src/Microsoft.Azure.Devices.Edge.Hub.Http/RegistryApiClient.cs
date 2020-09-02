// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class RegistryApiClient : IRegistryApiClient
    {
        internal const string MediaType = "application/json";
        const string PutModuleOnBehalfOfUriTemplate = "devices/{0}/modules/$edgeHub/putModuleOnBehalfOf?api-version={1}";
        const string GetModuleOnBehalfOfUriTemplate = "devices/{0}/modules/$edgeHub/getModuleOnBehalfOf?api-version={1}";
        const string ListModulesOnBehalfOfUriTemplate = "devices/{0}/getModulesOnTargetDevice?api-version={1}";
        const string DeleteModuleOnBehalfOfUriTemplate = "devices/{0}/deleteModuleOnBehalfOf?api-version={1}";
        const string ApiVersion = "2020-06-30-preview";

        readonly Option<IWebProxy> proxy;
        readonly ITokenProvider tokenProvider;
        readonly Uri baseUri;

        public RegistryApiClient(string upstreamHostname, Option<IWebProxy> proxy, ITokenProvider tokenProvider)
        {
            this.UpstreamHostName = Preconditions.CheckNonWhiteSpace(upstreamHostname, nameof(upstreamHostname));
            this.proxy = proxy;
            this.tokenProvider = Preconditions.CheckNotNull(tokenProvider, nameof(tokenProvider));

            this.baseUri = new UriBuilder(Uri.UriSchemeHttps, upstreamHostname).Uri;
        }

        internal string UpstreamHostName { get; }

        public async Task<HttpResponseMessage> PutModuleOnBehalfOfAsync(string actorEdgeDeviceId, CreateOrUpdateModuleOnBehalfOfData requestData)
        {
            var requesteUri = new Uri(this.baseUri, string.Format(CultureInfo.InvariantCulture, PutModuleOnBehalfOfUriTemplate, actorEdgeDeviceId, ApiVersion));
            var response = await this.SendRequestAsync(requesteUri, HttpMethod.Put, requestData, requestData.Module.ETag);
            return response;
        }

        public async Task<HttpResponseMessage> GetModuleOnBehalfOfAsync(string actorEdgeDeviceId, GetModuleOnBehalfOfData requestData)
        {
            var requesteUri = new Uri(this.baseUri, string.Format(CultureInfo.InvariantCulture, GetModuleOnBehalfOfUriTemplate, actorEdgeDeviceId, ApiVersion));
            var response = await this.SendRequestAsync(requesteUri, HttpMethod.Post, requestData);
            return response;
        }

        public async Task<HttpResponseMessage> ListModulesOnBehalfOfAsync(string actorEdgeDeviceId, ListModulesOnBehalfOfData requestData)
        {
            var requesteUri = new Uri(this.baseUri, string.Format(CultureInfo.InvariantCulture, ListModulesOnBehalfOfUriTemplate, actorEdgeDeviceId, ApiVersion));
            var response = await this.SendRequestAsync(requesteUri, HttpMethod.Post, requestData);
            return response;
        }

        public async Task<HttpResponseMessage> DeleteModuleOnBehalfOfAsync(string actorEdgeDeviceId, DeleteModuleOnBehalfOfData requestData)
        {
            var requesteUri = new Uri(this.baseUri, string.Format(CultureInfo.InvariantCulture, DeleteModuleOnBehalfOfUriTemplate, actorEdgeDeviceId, ApiVersion));
            var response = await this.SendRequestAsync(requesteUri, HttpMethod.Post, requestData);
            return response;
        }

        async Task<HttpResponseMessage> SendRequestAsync<T>(Uri uri, HttpMethod method, T payload, string etag = null)
        {
            HttpClient client = this.proxy
                .Map(p => new HttpClient(new HttpClientHandler { Proxy = p }, disposeHandler: true))
                .GetOrElse(() => new HttpClient());

            using (var request = new HttpRequestMessage(method, uri))
            {
                string token = await this.tokenProvider.GetTokenAsync(Option.None<TimeSpan>());
                request.Headers.Add(HttpRequestHeader.Authorization.ToString(), token);

                if (!string.IsNullOrEmpty(etag))
                {
                    request.Headers.Add("If-Match", etag);
                }

                request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, MediaType);

                HttpResponseMessage response = await client.SendAsync(request);
                return response;
            }
        }
    }

    public class CreateOrUpdateModuleOnBehalfOfData
    {
        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; set; }

        [JsonProperty(PropertyName = "module", Required = Required.Always)]
        public Module Module { get; set; }
    }

    public class GetModuleOnBehalfOfData
    {
        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; set; }

        [JsonProperty(PropertyName = "targetModuleId", Required = Required.Always)]
        public string ModuleId { get; set; }
    }

    public class ListModulesOnBehalfOfData
    {
        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; set; }
    }

    public class DeleteModuleOnBehalfOfData
    {
        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; set; }

        [JsonProperty(PropertyName = "moduleId", Required = Required.Always)]
        public string ModuleId { get; set; }
    }
}
