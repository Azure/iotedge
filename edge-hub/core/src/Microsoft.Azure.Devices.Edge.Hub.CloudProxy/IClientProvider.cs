// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IClientProvider
    {
        IClient Create(IIdentity identity, IAuthenticationMethod authenticationMethod, IotHubClientOptions options, Option<string> modelId);

        IClient Create(IIdentity identity, string connectionString, IotHubClientOptions options);

        Task<IClient> CreateAsync(IIdentity identity, IotHubClientOptions options);

        IClient Create(IIdentity identity, ITokenProvider tokenProvider, IotHubClientOptions options, Option<string> modelId);
    }
}
