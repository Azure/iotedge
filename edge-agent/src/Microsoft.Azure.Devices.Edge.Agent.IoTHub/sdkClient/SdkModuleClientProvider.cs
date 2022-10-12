// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public class SdkModuleClientProvider : ISdkModuleClientProvider
    {
        public ISdkModuleClient GetSdkModuleClient(string connectionString, ITransportSettings settings, TimeSpan cloudConnectionHangingTimeout)
        {
            ModuleClient moduleClient = ModuleClient.CreateFromConnectionString(connectionString, new[] { settings });
            return new WrappingSdkModuleClient(moduleClient, cloudConnectionHangingTimeout);
        }

        public async Task<ISdkModuleClient> GetSdkModuleClient(ITransportSettings settings, TimeSpan cloudConnectionHangingTimeout)
        {
            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(new[] { settings });
            return new WrappingSdkModuleClient(moduleClient, cloudConnectionHangingTimeout);
        }
    }
}
