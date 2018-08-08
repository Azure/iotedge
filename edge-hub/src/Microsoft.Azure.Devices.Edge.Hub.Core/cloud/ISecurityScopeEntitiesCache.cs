// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ISecurityScopeEntitiesCache : IDisposable
    {
        Task<Option<ServiceIdentity>> GetServiceIdentity(string id);
    }
}
