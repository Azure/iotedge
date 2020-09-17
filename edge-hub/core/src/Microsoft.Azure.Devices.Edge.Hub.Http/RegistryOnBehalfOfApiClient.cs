// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class RegistryOnBehalfOfApiClient : IRegistryOnBehalfOfApiClient
    {
        const string ApiVersion = "2020-06-30-preview";

        readonly string putModuleOnBehalfOfUriTemplate = $"devices/{{0}}/modules/{Constants.EdgeHubModuleId}/putModuleOnBehalfOf?api-version={{1}}";
        readonly string getModuleOnBehalfOfUriTemplate = $"devices/{{0}}/modules/{Constants.EdgeHubModuleId}/getModuleOnBehalfOf?api-version={{1}}";
        readonly string listModulesOnBehalfOfUriTemplate = $"devices/{{0}}/modules/{Constants.EdgeHubModuleId}/getModulesOnTargetDevice?api-version={{1}}";
        readonly string deleteModuleOnBehalfOfUriTemplate = $"devices/{{0}}/modules/{Constants.EdgeHubModuleId}/deleteModuleOnBehalfOf?api-version={{1}}";

        readonly Option<IWebProxy> proxy;
        readonly ITokenProvider tokenProvider;
        readonly Uri baseUri;

        public RegistryOnBehalfOfApiClient(string upstreamHostname, Option<IWebProxy> proxy, ITokenProvider tokenProvider)
        {
            Preconditions.CheckNonWhiteSpace(upstreamHostname, nameof(upstreamHostname));
            this.proxy = proxy;
            this.tokenProvider = Preconditions.CheckNotNull(tokenProvider, nameof(tokenProvider));

            this.baseUri = new UriBuilder(Uri.UriSchemeHttps, upstreamHostname).Uri;
        }

        public async Task<RegistryApiHttpResult> PutModuleAsync(string actorEdgeDeviceId, CreateOrUpdateModuleOnBehalfOfData requestData, string eTag)
        {
            var requesteUri = new Uri(this.baseUri, string.Format(CultureInfo.InvariantCulture, this.putModuleOnBehalfOfUriTemplate, actorEdgeDeviceId, ApiVersion));
            var response = await this.SendRequestAsync(requesteUri, HttpMethod.Post, requestData, eTag);
            return response;
        }

        public async Task<RegistryApiHttpResult> GetModuleAsync(string actorEdgeDeviceId, GetModuleOnBehalfOfData requestData)
        {
            var requesteUri = new Uri(this.baseUri, string.Format(CultureInfo.InvariantCulture, this.getModuleOnBehalfOfUriTemplate, actorEdgeDeviceId, ApiVersion));
            var response = await this.SendRequestAsync(requesteUri, HttpMethod.Post, requestData);
            return response;
        }

        public async Task<RegistryApiHttpResult> ListModulesAsync(string actorEdgeDeviceId, ListModulesOnBehalfOfData requestData)
        {
            var requesteUri = new Uri(this.baseUri, string.Format(CultureInfo.InvariantCulture, this.listModulesOnBehalfOfUriTemplate, actorEdgeDeviceId, ApiVersion));
            var response = await this.SendRequestAsync(requesteUri, HttpMethod.Post, requestData);
            return response;
        }

        public async Task<RegistryApiHttpResult> DeleteModuleAsync(string actorEdgeDeviceId, DeleteModuleOnBehalfOfData requestData)
        {
            var requesteUri = new Uri(this.baseUri, string.Format(CultureInfo.InvariantCulture, this.deleteModuleOnBehalfOfUriTemplate, actorEdgeDeviceId, ApiVersion));
            var response = await this.SendRequestAsync(requesteUri, HttpMethod.Post, requestData);
            return response;
        }

        async Task<RegistryApiHttpResult> SendRequestAsync<T>(Uri uri, HttpMethod method, T payload, string ifMatchHeader = null)
        {
            string token = await this.tokenProvider.GetTokenAsync(Option.None<TimeSpan>());

            using (HttpClient client = this.proxy
                .Map(p => new HttpClient(new HttpClientHandler { Proxy = p }, disposeHandler: true))
                .GetOrElse(() => new HttpClient()))
            using (var request = new HttpRequestMessage(method, uri))
            {
                string jsonPayload = JsonConvert.SerializeObject(payload);
                using (var stringContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
                {
                    request.Headers.Add(HttpRequestHeader.Authorization.ToString(), token);
                    if (!string.IsNullOrEmpty(ifMatchHeader))
                    {
                        request.Headers.Add("if-match", ifMatchHeader);
                    }

                    request.Content = stringContent;

                    using (var response = await client.SendAsync(request))
                    {
                        string jsonContent = string.Empty;
                        if (response.Content != null)
                        {
                            jsonContent = await response.Content.ReadAsStringAsync();
                        }

                        return new RegistryApiHttpResult(response.StatusCode, jsonContent);
                    }
                }
            }
        }
    }

    public class RegistryApiHttpResult
    {
        public RegistryApiHttpResult(HttpStatusCode statusCode, string jsonContent)
        {
            this.StatusCode = statusCode;
            this.JsonContent = jsonContent ?? string.Empty;
        }

        public HttpStatusCode StatusCode { get; private set; }

        public string JsonContent { get; private set; }
    }

    public class CreateOrUpdateModuleOnBehalfOfData
    {
        public CreateOrUpdateModuleOnBehalfOfData(string authChain, Module module)
        {
            this.AuthChain = authChain;
            this.Module = module;
        }

        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; private set; }

        [JsonProperty(PropertyName = "module", Required = Required.Always)]
        public Module Module { get; private set; }
    }

    public class GetModuleOnBehalfOfData
    {
        public GetModuleOnBehalfOfData(string authChain, string moduleId)
        {
            this.AuthChain = authChain;
            this.ModuleId = moduleId;
        }

        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; private set; }

        [JsonProperty(PropertyName = "targetModuleId", Required = Required.Always)]
        public string ModuleId { get; private set; }
    }

    public class ListModulesOnBehalfOfData
    {
        public ListModulesOnBehalfOfData(string authChain)
        {
            this.AuthChain = authChain;
        }

        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; private set; }
    }

    public class DeleteModuleOnBehalfOfData
    {
        public DeleteModuleOnBehalfOfData(string authChian, string moduleId)
        {
            this.AuthChain = authChian;
            this.ModuleId = moduleId;
        }

        [JsonProperty(PropertyName = "authChain", Required = Required.Always)]
        public string AuthChain { get; private set; }

        [JsonProperty(PropertyName = "moduleId", Required = Required.Always)]
        public string ModuleId { get; private set; }
    }
}
