// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IClientProvider
    {
        IClient Create(IIdentity identity, IAuthenticationMethod authenticationMethod, ITransportSettings[] transportSettings);

        IClient Create(IIdentity identity, string connectionString, ITransportSettings[] transportSettings);

        Task<IClient> CreateAsync(IIdentity identity, ITransportSettings[] transportSettings);

        IClient Create(IIdentity identity, ITokenProvider tokenProvider, ITransportSettings[] transportSettings);
    }
}
