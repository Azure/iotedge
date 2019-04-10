// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Azure.Devices.Shared;

namespace common
{
    public class EdgeDeviceIdentity
    {
        Option<Device> device;
        Option<string> deviceConnectionString;
        string hubConnectionString;

        public Option<string> ConnectionString => this.deviceConnectionString;

        public EdgeDeviceIdentity(string hubConnectionString)
        {
            this.device = Option.None<Device>();
            this.deviceConnectionString = Option.None<string>();
            this.hubConnectionString = hubConnectionString;
        }

        public async Task CreateAsync(string deviceId, CancellationToken token)
        {
            var settings = new HttpTransportSettings();
            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.hubConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(builder.ToString(), settings);

            var device = new Device(deviceId)
            {
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas },
                Capabilities = new DeviceCapabilities() { IotEdge = true }
            };

            device = await rm.AddDeviceAsync(device, token);
            this.device = Option.Some(device);
            this.deviceConnectionString = Option.Some(
                $"HostName={builder.HostName};" +
                $"DeviceId={device.Id};" +
                $"SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}"
            );
            Console.WriteLine($">>> Device connection string: {this.deviceConnectionString}");
        }
    }
}