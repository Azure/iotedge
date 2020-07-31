// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ClientProvider : IClientProvider
    {
        readonly Option<string> gatewayHostname;

        public ClientProvider(Option<string> gatewayHostname)
        {
            this.gatewayHostname = gatewayHostname;
        }

        public IClient Create(IIdentity identity, IAuthenticationMethod authenticationMethod, ITransportSettings[] transportSettings, Option<string> modelId)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(transportSettings, nameof(transportSettings));
            Preconditions.CheckNotNull(authenticationMethod, nameof(authenticationMethod));
            modelId.ForEach(m => Preconditions.CheckNonWhiteSpace(m, nameof(m)));

            Option<ClientOptions> options = modelId.Match(
                m => Option.Some(new ClientOptions { ModelId = m }),
                () => Option.None<ClientOptions>());

            if (identity is IModuleIdentity)
            {
                ModuleClient moduleClient = this.gatewayHostname.Match(
                    v =>
                    {
                        return options.Match(
                            o => ModuleClient.Create(identity.IotHubHostname, v, authenticationMethod, transportSettings, o),
                            () => ModuleClient.Create(identity.IotHubHostname, v, authenticationMethod, transportSettings));
                    },
                    () =>
                    {
                        return options.Match(
                            o => ModuleClient.Create(identity.IotHubHostname, authenticationMethod, transportSettings, o),
                            () => ModuleClient.Create(identity.IotHubHostname, authenticationMethod, transportSettings));
                    });
                return new ModuleClientWrapper(moduleClient);
            }
            else if (identity is IDeviceIdentity)
            {
                DeviceClient deviceClient = this.gatewayHostname.Match(
                v =>
                {
                    return options.Match(
                        o => DeviceClient.Create(identity.IotHubHostname, v, authenticationMethod, transportSettings, o),
                        () => DeviceClient.Create(identity.IotHubHostname, v, authenticationMethod, transportSettings));
                },
                () =>
                {
                    return options.Match(
                        o => DeviceClient.Create(identity.IotHubHostname, authenticationMethod, transportSettings, o),
                        () => DeviceClient.Create(identity.IotHubHostname, authenticationMethod, transportSettings));
                });
                return new DeviceClientWrapper(deviceClient);
            }

            throw new InvalidOperationException($"Invalid client identity type {identity.GetType()}");
        }

        public IClient Create(IIdentity identity, string connectionString, ITransportSettings[] transportSettings)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(transportSettings, nameof(transportSettings));
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));

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
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(transportSettings, nameof(transportSettings));

            if (!(identity is IModuleIdentity))
            {
                throw new InvalidOperationException($"Invalid client identity type {identity.GetType()}. CreateFromEnvironment supports only ModuleIdentity");
            }

            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(transportSettings);
            return new ModuleClientWrapper(moduleClient);
        }

        public IClient Create(IIdentity identity, ITokenProvider tokenProvider, ITransportSettings[] transportSettings, Option<string> modelId)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(transportSettings, nameof(transportSettings));
            Preconditions.CheckNotNull(tokenProvider, nameof(tokenProvider));

            switch (identity)
            {
                case IModuleIdentity moduleIdentity:
                    return this.Create(identity, new ModuleAuthentication(tokenProvider, moduleIdentity.DeviceId, moduleIdentity.ModuleId), transportSettings, modelId);

                case IDeviceIdentity deviceIdentity:
                    return this.Create(identity, new DeviceAuthentication(tokenProvider, deviceIdentity.DeviceId), transportSettings, modelId);

                default:
                    throw new InvalidOperationException($"Invalid client identity type {identity.GetType()}");
            }
        }
    }
}
