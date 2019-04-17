// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace common
{
    public class EdgeDevice
    {
        Option<DeviceContext> context;
        string deviceId;
        IotHub iotHub;

        public DeviceContext Context => this.context.Expect(
            () => new InvalidOperationException(
                $"No context for unknown device '{this.deviceId}'. Call " +
                "[GetOr]CreateAsync() first."
            )
        );

        public EdgeDevice(string deviceId, string hubConnectionString)
        {
            this.iotHub = new IotHub(hubConnectionString);
            this.context = Option.None<DeviceContext>();
            this.deviceId = deviceId;
        }

        public Task CreateIdentityAsync(CancellationToken token)
        {
            return Profiler.Run(
                $"Creating edge device '{this.deviceId}' on hub '{this.iotHub.Hostname}'",
                async () => {
                    Device device = await this.iotHub.CreateEdgeDeviceIdentity(this.deviceId, token);
                    var context = new DeviceContext(device, true, this.iotHub);
                    this.context = Option.Some(context);
                }
            );
        }

        public async Task GetOrCreateIdentityAsync(CancellationToken token)
        {
            Device device = await this.iotHub.GetDeviceIdentityAsync(this.deviceId, token);
            if (device != null)
            {
                if (!device.Capabilities.IotEdge)
                {
                    throw new InvalidOperationException(
                        $"Device '{device.Id}' exists, but is not an edge device"
                    );
                }

                var context = new DeviceContext(device, false, this.iotHub);
                this.context = Option.Some(context);

                Console.WriteLine($"Device '{device.Id}' already exists on hub '{this.iotHub.Hostname}'");
            }
            else
            {
                await this.CreateIdentityAsync(token);
            }
        }

        public Task DeleteIdentityAsync(CancellationToken token)
        {
            DeviceContext context = _GetContext("Cannot delete");
            return _DeleteIdentityAsync(context, token);
        }

        public async Task MaybeDeleteIdentityAsync(CancellationToken token)
        {
            DeviceContext context = _GetContext("Cannot delete");
            if (context.Owned)
            {
                await _DeleteIdentityAsync(context, token);
            }
            else
            {
                Console.WriteLine($"Pre-existing device '{context.Device.Id}' was not deleted");
            }
        }

        static Task _DeleteIdentityAsync(DeviceContext context, CancellationToken token)
        {
            return Profiler.Run(
                $"Deleting device '{context.Device.Id}'",
                () => context.IotHub.DeleteDeviceIdentityAsync(context.Device, token)
            );
        }

        DeviceContext _GetContext(string errorPrefix)
        {
            return this.context.Expect(
                () => new InvalidOperationException(
                    $"{errorPrefix} unknown device '{this.deviceId}'. Call " +
                    "[GetOr]CreateAsync() first."
                )
            );
        }
    }
}