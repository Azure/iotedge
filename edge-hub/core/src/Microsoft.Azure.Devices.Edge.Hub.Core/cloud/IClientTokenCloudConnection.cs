// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Cloud
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public interface IClientTokenCloudConnection : ICloudConnection
    {
        Task<ICloudProxy> UpdateTokenAsync(ITokenCredentials tokenCredentials);
    }
}
