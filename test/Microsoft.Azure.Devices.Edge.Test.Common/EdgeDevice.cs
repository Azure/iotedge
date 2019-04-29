// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Serilog;

    public class EdgeDevice
    {
        readonly Device device;
        readonly IotHub iotHub;
        readonly bool owned;

        EdgeDevice(Device device, bool owned, IotHub iotHub)
        {
            this.device = device;
            this.iotHub = iotHub;
            this.owned = owned;
        }

        public string ConnectionString =>
            $"HostName={this.iotHub.Hostname};" +
            $"DeviceId={this.device.Id};" +
            $"SharedAccessKey={this.device.Authentication.SymmetricKey.PrimaryKey}";

        public string Id => this.device.Id;

        public static Task<EdgeDevice> CreateIdentityAsync(
            string deviceId,
            IotHub iotHub,
            CancellationToken token)
        {
            return Profiler.Run(
                async () =>
                {
                    Device device = await iotHub.CreateEdgeDeviceIdentityAsync(deviceId, token);
                    return new EdgeDevice(device, true, iotHub);
                },
                "Created edge device '{Device}' on hub '{IotHub}'",
                deviceId,
                iotHub.Hostname);
        }

        public static async Task<EdgeDevice> GetOrCreateIdentityAsync(
            string deviceId,
            IotHub iotHub,
            CancellationToken token)
        {
            Device device = await iotHub.GetDeviceIdentityAsync(deviceId, token);
            if (device != null)
            {
                if (!device.Capabilities.IotEdge)
                {
                    throw new InvalidOperationException($"Device '{device.Id}' exists, but is not an edge device");
                }

                Log.Information(
                    "Device '{Device}' already exists on hub '{IotHub}'",
                    device.Id,
                    iotHub.Hostname);
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
                () => this.iotHub.DeleteDeviceIdentityAsync(this.device, token),
                "Deleted device '{Device}'",
                this.Id);
        }

        public async Task MaybeDeleteIdentityAsync(CancellationToken token)
        {
            if (this.owned)
            {
                await this.DeleteIdentityAsync(token);
            }
            else
            {
                Log.Information("Pre-existing device '{Device}' was not deleted", this.Id);
            }
        }
    }
}
