// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http
{
    using System.Net.Http;
    using System.Threading.Tasks;

    public interface IRegistryOnBehalfOfApiClient
    {
        Task<RegistryApiHttpResult> PutModuleAsync(string actorDeviceId, CreateOrUpdateModuleOnBehalfOfData requestData, string ifMatchHeader);

        Task<RegistryApiHttpResult> GetModuleAsync(string actorDeviceId, GetModuleOnBehalfOfData requestData);

        Task<RegistryApiHttpResult> ListModulesAsync(string actorDeviceId, ListModulesOnBehalfOfData requestData);

        Task<RegistryApiHttpResult> DeleteModuleAsync(string actorDeviceId, DeleteModuleOnBehalfOfData requestData);
    }
}
