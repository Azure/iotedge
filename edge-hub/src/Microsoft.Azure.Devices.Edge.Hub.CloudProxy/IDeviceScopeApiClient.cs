// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Threading.Tasks;

    public interface IDeviceScopeApiClient
    {
        Task<ScopeResult> GetIdentitiesInScope();

        Task<ScopeResult> GetIdentity(string deviceId, string moduleId);

        Task<ScopeResult> GetNext(string continuationToken);
    }
}
