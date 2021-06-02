// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;

    public interface IServiceIdentitiesIterator
    {
        bool HasNext { get; }

        Task<IEnumerable<ServiceIdentity>> GetNext();
    }
}
