// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Net.Http;
    using System.Threading.Tasks;

    public interface IRegistryOnBehalfOfApiClient
    {
        Task<RegistryApiHttpResult> PutModuleAsync(string actorEdgeDeviceId, CreateOrUpdateModuleOnBehalfOfData requestData, string ifMatchHeader);

        Task<RegistryApiHttpResult> GetModuleAsync(string actorEdgeDeviceId, GetModuleOnBehalfOfData requestData);

        Task<RegistryApiHttpResult> ListModulesAsync(string actorEdgeDeviceId, ListModulesOnBehalfOfData requestData);

        Task<RegistryApiHttpResult> DeleteModuleAsync(string actorEdgeDeviceId, DeleteModuleOnBehalfOfData requestData);
    }
}
