// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
