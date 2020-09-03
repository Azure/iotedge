// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IDeviceScopeApiClient
    {
        string TargetEdgeDeviceId { get;  }

        Task<ScopeResult> GetIdentitiesInScopeAsync();

        Task<ScopeResult> GetNextAsync(string continuationToken);

        Task<ScopeResult> GetIdentityAsync(string deviceId, string moduleId);

        Task<ScopeResult> GetIdentityOnBehalfOfAsync(string deviceId, Option<string> moduleId, string onBehalfOfDevice);
    }
}
