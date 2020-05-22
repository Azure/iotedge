// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public interface IDeviceScopeApiClient
    {
        string TargetEdgeDeviceId { get;  }

        IDeviceScopeApiClient CreateOnBehalfOfDeviceScopeClient(string deviceId);

        Task<ScopeResult> GetIdentitiesInScope();

        Task<ScopeResult> GetNext(string continuationToken);

        Task<ScopeResult> GetIdentity(string deviceId, string moduleId);
    }
}
