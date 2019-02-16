// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ConnectivityAwareClientProvider : IClientProvider
    {
        readonly IDeviceConnectivityManager deviceConnectivityManager;
        readonly IClientProvider underlyingClientProvider;

        public ConnectivityAwareClientProvider(IClientProvider underlyingProvider, IDeviceConnectivityManager deviceConnectivityManager)
        {
            this.underlyingClientProvider = Preconditions.CheckNotNull(underlyingProvider, nameof(underlyingProvider));
            this.deviceConnectivityManager = Preconditions.CheckNotNull(deviceConnectivityManager, nameof(deviceConnectivityManager));
        }

        public IClient Create(IIdentity identity, IAuthenticationMethod authenticationMethod, ITransportSettings[] transportSettings) =>
            new ConnectivityAwareClient(this.underlyingClientProvider.Create(identity, authenticationMethod, transportSettings), this.deviceConnectivityManager, identity);

        public IClient Create(IIdentity identity, string connectionString, ITransportSettings[] transportSettings) =>
            new ConnectivityAwareClient(this.underlyingClientProvider.Create(identity, connectionString, transportSettings), this.deviceConnectivityManager, identity);

        public async Task<IClient> CreateAsync(IIdentity identity, ITransportSettings[] transportSettings) =>
            new ConnectivityAwareClient(await this.underlyingClientProvider.CreateAsync(identity, transportSettings), this.deviceConnectivityManager, identity);

        public IClient Create(IIdentity identity, ITokenProvider tokenProvider, ITransportSettings[] transportSettings) =>
            new ConnectivityAwareClient(this.underlyingClientProvider.Create(identity, tokenProvider, transportSettings), this.deviceConnectivityManager, identity);
    }
}
