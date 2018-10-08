// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
