// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;

    public interface IServiceIdentitiesIterator
    {
        Task<IEnumerable<ServiceIdentity>> GetNext();

        bool HasNext { get; }
    }
}
