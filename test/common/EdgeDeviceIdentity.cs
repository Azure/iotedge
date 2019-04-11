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
        class DeviceContext
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
        }

        Option<DeviceContext> context;
        string deviceId;
        string hubConnectionString;

        public Option<string> ConnectionString => this.context.Map(c => c.ConnectionString);

        public EdgeDeviceIdentity(string deviceId, string hubConnectionString)
        {
            this.context = Option.None<DeviceContext>();
            this.deviceId = deviceId;
            this.hubConnectionString = hubConnectionString;
        }

        public async Task CreateAsync(CancellationToken token)
        {
            var settings = new HttpTransportSettings();
            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.hubConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(builder.ToString(), settings);

            await this._CreateAsync(rm, builder.HostName, token);
        }

        public async Task GetOrCreateAsync(CancellationToken token)
        {
            var settings = new HttpTransportSettings();
            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.hubConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(builder.ToString(), settings);

            Device device = await rm.GetDeviceAsync(this.deviceId, token);
            if (device != null)
            {
                if (!device.Capabilities.IotEdge)
                {
                    throw new InvalidOperationException(
                        $"Device '{device.Id}' exists, but is not an edge device"
                    );
                }

                var context = new DeviceContext(device, builder.HostName, false, rm);
                this.context = Option.Some(context);

                Console.WriteLine($"Device '{device.Id}' already exists on hub '{builder.HostName}'");
            }
            else
            {
                await this._CreateAsync(rm, builder.HostName, token);
            }
        }

        async Task _CreateAsync(RegistryManager rm, string hostname, CancellationToken token)
        {
            var device = new Device(this.deviceId)
            {
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas },
                Capabilities = new DeviceCapabilities() { IotEdge = true }
            };
            device = await rm.AddDeviceAsync(device, token);

            var context = new DeviceContext(device, hostname, true, rm);
            this.context = Option.Some(context);

            Console.WriteLine($"Edge device '{device.Id}' was created on hub '{hostname}'");
        }

        public async Task DeleteAsync(CancellationToken token)
        {
            DeviceContext context = _GetContextForDelete();
            await this._DeleteAsync(context, token);
        }

        public async Task MaybeDeleteAsync(CancellationToken token)
        {
            DeviceContext context = _GetContextForDelete();
            if (context.Owned)
            {
                await this._DeleteAsync(context, token);
            }
            else
            {
                Console.WriteLine($"Pre-existing device '{context.Device.Id}' was not deleted");
            }
        }

        DeviceContext _GetContextForDelete()
        {
            return this.context.Expect(
                () => new InvalidOperationException(
                    $"Cannot delete unknown device '{this.deviceId}'. Call " +
                    "[GetOr]CreateAsync() first."
                )
            );
        }

        async Task _DeleteAsync(DeviceContext context, CancellationToken token)
        {
            await context.Registry.RemoveDeviceAsync(context.Device);
            Console.WriteLine($"Device '{context.Device.Id}' was deleted");
        }
    }
}