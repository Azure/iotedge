// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IDeviceScopeIdentitiesCache
    {
        event EventHandler<string> ServiceIdentityRemoved;

        event EventHandler<ServiceIdentity> ServiceIdentityUpdated;

        Task<Option<ServiceIdentity>> GetServiceIdentity(string id, bool refreshIfNotExists = false);

        void InitiateCacheRefresh();

        Task RefreshServiceIdentities(IEnumerable<string> ids);

        Task RefreshServiceIdentity(string id);
    }
}
