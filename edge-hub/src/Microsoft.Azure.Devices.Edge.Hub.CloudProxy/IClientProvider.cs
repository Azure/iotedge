// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface IClientProvider
    {
        IClient Create(IIdentity identity, IAuthenticationMethod authenticationMethod, ITransportSettings[] transportSettings);

        IClient Create(IIdentity identity, string connectionString, ITransportSettings[] transportSettings);

        Task<IClient> CreateAsync(IIdentity identity, ITransportSettings[] transportSettings);
    }
}
