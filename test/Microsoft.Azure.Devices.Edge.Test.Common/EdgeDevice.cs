// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;

    public class EdgeDevice
    {
        readonly Device device;
        readonly IotHub iotHub;
        readonly bool owned;

        public string ConnectionString =>
            $"HostName={this.iotHub.Hostname};" +
            $"DeviceId={this.device.Id};" +
            $"SharedAccessKey={this.device.Authentication.SymmetricKey.PrimaryKey}";

        public string Id => this.device.Id;

        EdgeDevice(Device device, bool owned, IotHub iotHub)
        {
            this.device = device;
            this.iotHub = iotHub;
            this.owned = owned;
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
                    Device device = await iotHub.CreateEdgeDeviceIdentityAsync(deviceId, token);
                    return new EdgeDevice(device, true, iotHub);
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
                return new EdgeDevice(device, false, iotHub);
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
                () => this.iotHub.DeleteDeviceIdentityAsync(this.device, token)
            );
        }

        public async Task MaybeDeleteIdentityAsync(CancellationToken token)
        {
            if (this.owned)
            {
                await this.DeleteIdentityAsync(token);
            }
            else
            {
                Console.WriteLine($"Pre-existing device '{this.Id}' was not deleted");
            }
        }
    }
}
