// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public interface ISdkModuleClientProvider
    {
        ISdkModuleClient GetSdkModuleClient(string connectionString, ITransportSettings settings, TimeSpan cloudConnectionHangingTimeout);

        Task<ISdkModuleClient> GetSdkModuleClient(ITransportSettings settings, TimeSpan cloudConnectionHangingTimeout);
    }
}
