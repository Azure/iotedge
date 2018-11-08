// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public class ClientProvider : IClientProvider
    {
        public IClient Create(IIdentity identity, IAuthenticationMethod authenticationMethod, ITransportSettings[] transportSettings)
        {
            if (identity is IModuleIdentity)
            {
                ModuleClient moduleClient = ModuleClient.Create(identity.IotHubHostName, authenticationMethod, transportSettings);
                return new ModuleClientWrapper(moduleClient);
            }
            else if (identity is IDeviceIdentity)
            {
                DeviceClient deviceClient = DeviceClient.Create(identity.IotHubHostName, authenticationMethod, transportSettings);
                return new DeviceClientWrapper(deviceClient);
            }

            throw new InvalidOperationException($"Invalid client identity type {identity.GetType()}");
        }

        public IClient Create(IIdentity identity, string connectionString, ITransportSettings[] transportSettings)
        {
            if (identity is IModuleIdentity)
            {
                ModuleClient moduleClient = ModuleClient.CreateFromConnectionString(connectionString, transportSettings);
                return new ModuleClientWrapper(moduleClient);
            }
            else if (identity is IDeviceIdentity)
            {
                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, transportSettings);
                return new DeviceClientWrapper(deviceClient);
            }

            throw new InvalidOperationException($"Invalid client identity type {identity.GetType()}");
        }

        public async Task<IClient> CreateAsync(IIdentity identity, ITransportSettings[] transportSettings)
        {
            if (!(identity is IModuleIdentity))
            {
                throw new InvalidOperationException($"Invalid client identity type {identity.GetType()}. CreateFromEnvironment supports only ModuleIdentity");
            }

            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(transportSettings).ConfigureAwait(false);
            return new ModuleClientWrapper(moduleClient);
        }
    }
}
