// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;

    // TODO: implement satellite
    class SatelliteController : INetworkController
    {
        string name;

        public SatelliteController(string name)
        {
            this.name = name;
        }

        public string Description => "Satellite";

        public Task<bool> SetStatusAsync(NetworkStatus status, CancellationToken cs)
        {
            return Task.FromResult(true);
        }

        public Task<NetworkStatus> GetStatusAsync(CancellationToken cs)
        {
            return Task.FromResult(NetworkStatus.Default);
        }
    }
}
