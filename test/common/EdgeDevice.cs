// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Azure.Devices.Shared;

namespace common
{
    public class EdgeDevice
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

        public EdgeDevice(string deviceId, string hubConnectionString)
        {
            this.context = Option.None<DeviceContext>();
            this.deviceId = deviceId;
            this.hubConnectionString = hubConnectionString;
        }

        public Task<string> CreateIdentityAsync(CancellationToken token)
        {
            var settings = new HttpTransportSettings();
            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.hubConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(builder.ToString(), settings);

            return this._CreateIdentityAsync(rm, builder.HostName, token);
        }

        public async Task<string> GetOrCreateIdentityAsync(CancellationToken token)
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
                return context.ConnectionString;
            }
            else
            {
                return await this._CreateIdentityAsync(rm, builder.HostName, token);
            }
        }

        Task<string> _CreateIdentityAsync(RegistryManager rm, string hub, CancellationToken token)
        {
            return Profiler.Run(
                $"Creating edge device '{this.deviceId}' on hub '{hub}'",
                async () => {
                    var device = new Device(this.deviceId)
                    {
                        Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas },
                        Capabilities = new DeviceCapabilities() { IotEdge = true }
                    };
                    device = await rm.AddDeviceAsync(device, token);

                    var context = new DeviceContext(device, hub, true, rm);
                    this.context = Option.Some(context);
                    return context.ConnectionString;
                }
            );
        }

        public Task DeleteIdentityAsync(CancellationToken token)
        {
            DeviceContext context = _GetContextForDelete();
            return this._DeleteIdentityAsync(context, token);
        }

        public async Task MaybeDeleteIdentityAsync(CancellationToken token)
        {
            DeviceContext context = _GetContextForDelete();
            if (context.Owned)
            {
                await this._DeleteIdentityAsync(context, token);
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

        Task _DeleteIdentityAsync(DeviceContext context, CancellationToken token)
        {
            return Profiler.Run(
                $"Deleting device '{context.Device.Id}'",
                () => context.Registry.RemoveDeviceAsync(context.Device)
            );
        }
    }
}