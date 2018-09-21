// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IServiceProxy
    {
        IServiceIdentitiesIterator GetServiceIdentitiesIterator();

        Task<Option<ServiceIdentity>> GetServiceIdentity(string deviceId, string moduleId);

        Task<Option<ServiceIdentity>> GetServiceIdentity(string id);
    }
}
