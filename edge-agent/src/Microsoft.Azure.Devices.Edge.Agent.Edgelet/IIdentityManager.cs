// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;

    public interface IIdentityManager
    {
        Task<Identity> CreateIdentityAsync(string name);

        Task DeleteIdentityAsync(string name);

        Task<IEnumerable<Identity>> GetIdentities();
    }
}
