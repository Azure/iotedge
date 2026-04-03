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

        public IClient Create(IIdentity identity, IAuthenticationMethod authenticationMethod, IotHubClientOptions options, Option<string> modelId)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(options, nameof(options));
            Preconditions.CheckNotNull(authenticationMethod, nameof(authenticationMethod));
            modelId.ForEach(m => Preconditions.CheckNonWhiteSpace(m, nameof(m)));

            modelId.ForEach(m => options.ModelId = m);

            this.gatewayHostname.ForEach(v => options.GatewayHostName = v);

            if (identity is IModuleIdentity)
            {
                var moduleClient = new IotHubModuleClient(identity.IotHubHostname, authenticationMethod, options);
                return new ModuleClientWrapper(moduleClient);
            }
            else if (identity is IDeviceIdentity)
            {
                var deviceClient = new IotHubDeviceClient(identity.IotHubHostname, authenticationMethod, options);
                return new DeviceClientWrapper(deviceClient);
            }

            throw new InvalidOperationException($"Invalid client identity type {identity.GetType()}");
        }

        public IClient Create(IIdentity identity, string connectionString, IotHubClientOptions options)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(options, nameof(options));
            Preconditions.CheckNonWhiteSpace(connectionString, nameof(connectionString));

            if (identity is IModuleIdentity)
            {
                var moduleClient = new IotHubModuleClient(connectionString, options);
                return new ModuleClientWrapper(moduleClient);
            }
            else if (identity is IDeviceIdentity)
            {
                var deviceClient = new IotHubDeviceClient(connectionString, options);
                return new DeviceClientWrapper(deviceClient);
            }

            throw new InvalidOperationException($"Invalid client identity type {identity.GetType()}");
        }

        public async Task<IClient> CreateAsync(IIdentity identity, IotHubClientOptions options)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(options, nameof(options));

            if (!(identity is IModuleIdentity))
            {
                throw new InvalidOperationException($"Invalid client identity type {identity.GetType()}. CreateFromEnvironment supports only ModuleIdentity");
            }

            IotHubModuleClient moduleClient = await IotHubModuleClient.CreateFromEnvironmentAsync(options);
            return new ModuleClientWrapper(moduleClient);
        }

        public IClient Create(IIdentity identity, ITokenProvider tokenProvider, IotHubClientOptions options, Option<string> modelId)
        {
            Preconditions.CheckNotNull(identity, nameof(identity));
            Preconditions.CheckNotNull(options, nameof(options));
            Preconditions.CheckNotNull(tokenProvider, nameof(tokenProvider));

            switch (identity)
            {
                case IModuleIdentity moduleIdentity:
                    return this.Create(identity, new ModuleAuthentication(tokenProvider, moduleIdentity.DeviceId, moduleIdentity.ModuleId), options, modelId);

                case IDeviceIdentity deviceIdentity:
                    return this.Create(identity, new DeviceAuthentication(tokenProvider, deviceIdentity.DeviceId), options, modelId);

                default:
                    throw new InvalidOperationException($"Invalid client identity type {identity.GetType()}");
            }
        }
    }
}
