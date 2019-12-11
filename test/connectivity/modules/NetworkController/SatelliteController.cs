// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System.Threading;
    using System.Threading.Tasks;

    // TODO: implement satellite
    class SatelliteController : IController
    {
        string name;

        public SatelliteController(string name)
        {
            this.name = name;
        }

        public string Description => "Satellite";

        public Task<bool> SetStatus(NetworkStatus status, CancellationToken cs)
        {
            return Task.FromResult(true);
        }

        public Task<NetworkStatus> GetStatus(CancellationToken cs)
        {
            return Task.FromResult(NetworkStatus.Default);
        }
    }
}
