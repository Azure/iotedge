// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface ICredentialsCache
    {
        Task Add(IClientCredentials clientCredentials);

        Task<Option<IClientCredentials>> Get(IIdentity identity);
    }
}
