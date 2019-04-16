// Copyright (c) Microsoft. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;

namespace common
{
    public class DeviceContext
    {
        public string ConnectionString { get; }
        public bool Owned { get; }
        public Device Device { get; }
        public RegistryManager Registry { get; }

        public DeviceContext(Device device, string hostname, bool owned, RegistryManager rm)
        {
            this.ConnectionString = 
                $"HostName={hostname};" +
                $"DeviceId={device.Id};" +
                $"SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";
            this.Owned = owned;
            this.Device = device;
            this.Registry = rm;
        }

        public Task DeployConfigurationAsync(ConfigurationContent config, CancellationToken token) =>
            this.Registry.ApplyConfigurationContentOnDeviceAsync(this.Device.Id, config, token);
    }
}