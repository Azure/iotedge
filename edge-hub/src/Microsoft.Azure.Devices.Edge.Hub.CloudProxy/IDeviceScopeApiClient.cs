// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Threading.Tasks;

    public interface IDeviceScopeApiClient
    {
        Task<ScopeResult> GetIdentitiesInScope();

        Task<ScopeResult> GetNext(string continuationToken);

        Task<ScopeResult> GetIdentity(string deviceId, string moduleId);
    }
}
