// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Net.Http;
    using System.Threading.Tasks;

    public interface IRegistryApiClient
    {
        public Task<HttpResponseMessage> PutModuleOnBehalfOfAsync(string actorEdgeDeviceId, CreateOrUpdateModuleOnBehalfOfData requestData);

        public Task<HttpResponseMessage> GetModuleOnBehalfOfAsync(string actorEdgeDeviceId, GetModuleOnBehalfOfData requestData);

        public Task<HttpResponseMessage> ListModulesOnBehalfOfAsync(string actorEdgeDeviceId, ListModulesOnBehalfOfData requestData);

        public Task<HttpResponseMessage> DeleteModuleOnBehalfOfAsync(string actorEdgeDeviceId, DeleteModuleOnBehalfOfData requestData);
    }
}
