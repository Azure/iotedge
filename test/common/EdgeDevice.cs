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
        DeviceContext context;
        IotHub iotHub;

        public string ConnectionString => this.context.ConnectionString;
        public string Id => this.context.Device.Id;

        EdgeDevice(DeviceContext context, IotHub iotHub)
        {
            this.context = context;
            this.iotHub = iotHub;
        }

        public static Task<EdgeDevice> CreateIdentityAsync(
            string deviceId,
            IotHub iotHub,
            CancellationToken token
        )
        {
            return Profiler.Run(
                $"Creating edge device '{deviceId}' on hub '{iotHub.Hostname}'",
                async () => {
                    Device device = await iotHub.CreateEdgeDeviceIdentity(deviceId, token);
                    var context = new DeviceContext(device, true, iotHub.Hostname);
                    return new EdgeDevice(context, iotHub);
                }
            );
        }

        public static async Task<EdgeDevice> GetOrCreateIdentityAsync(
            string deviceId,
            IotHub iotHub,
            CancellationToken token
        )
        {
            Device device = await iotHub.GetDeviceIdentityAsync(deviceId, token);
            if (device != null)
            {
                if (!device.Capabilities.IotEdge)
                {
                    throw new InvalidOperationException(
                        $"Device '{device.Id}' exists, but is not an edge device"
                    );
                }

                Console.WriteLine($"Device '{device.Id}' already exists on hub '{iotHub.Hostname}'");
                var context = new DeviceContext(device, false, iotHub.Hostname);
                return new EdgeDevice(context, iotHub);
            }
            else
            {
                return await CreateIdentityAsync(deviceId, iotHub, token);
            }
        }

        public Task DeleteIdentityAsync(CancellationToken token)
        {
            return Profiler.Run(
                $"Deleting device '{this.Id}'",
                () => this.iotHub.DeleteDeviceIdentityAsync(this.context.Device, token)
            );
        }

        public async Task MaybeDeleteIdentityAsync(CancellationToken token)
        {
            if (this.context.Owned)
            {
                await DeleteIdentityAsync(token);
            }
            else
            {
                Console.WriteLine($"Pre-existing device '{this.Id}' was not deleted");
            }
        }
    }
}