// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    interface IDeviceTokenCredentialsCache
    {
        Task Add(ITokenCredentials tokenCredentials);

        Task<Option<ITokenCredentials>> Get(IIdentity identity);
    }
}
