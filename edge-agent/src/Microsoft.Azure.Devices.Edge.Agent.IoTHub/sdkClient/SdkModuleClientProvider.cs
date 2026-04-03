// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public class SdkModuleClientProvider : ISdkModuleClientProvider
    {
        public ISdkModuleClient GetSdkModuleClient(string connectionString, IotHubClientOptions options)
        {
            var moduleClient = new IotHubModuleClient(connectionString, options);
            return new WrappingSdkModuleClient(moduleClient);
        }

        public async Task<ISdkModuleClient> GetSdkModuleClient(IotHubClientOptions options)
        {
            IotHubModuleClient moduleClient = await IotHubModuleClient.CreateFromEnvironmentAsync(options);
            return new WrappingSdkModuleClient(moduleClient);
        }
    }
}
