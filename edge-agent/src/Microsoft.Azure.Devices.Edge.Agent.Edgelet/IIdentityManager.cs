// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models;

    public interface IIdentityManager
    {
        Task<Identity> CreateIdentityAsync(string name, string managedBy);

        Task<Identity> UpdateIdentityAsync(string name, string generationId, string managedBy);

        Task DeleteIdentityAsync(string name);

        Task<IEnumerable<Identity>> GetIdentities();
    }
}
