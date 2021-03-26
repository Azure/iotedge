// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IServiceIdentityHierarchy
    {
        string GetActorDeviceId();

        Task InsertOrUpdate(ServiceIdentity identity);

        Task Remove(string id);

        Task<bool> Contains(string id);

        Task<Option<ServiceIdentity>> Get(string id);

        Task<IList<string>> GetAllIds();

        Task<Option<string>> GetAuthChain(string id);

        Task<Try<string>> TryGetAuthChain(string targetId);

        Task<Option<string>> GetEdgeAuthChain(string id);

        Task<IList<ServiceIdentity>> GetImmediateChildren(string id);
    }
}
