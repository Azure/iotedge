// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.Util;
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

        public string HubHostname => this.iotHub.Hostname;
        public string Id => this.device.Id;
        public string SharedAccessKey => this.device.Authentication.SymmetricKey.PrimaryKey;

        public static Task<EdgeDevice> CreateIdentityAsync(
            string deviceId,
            Option<string> parentDeviceId,
            IotHub iotHub,
            AuthenticationType authType,
            X509Thumbprint x509Thumbprint,
            CancellationToken token)
        {
            if (authType == AuthenticationType.SelfSigned && x509Thumbprint == null)
            {
                throw new ArgumentException("A device created with self-signed mechanism must provide an x509 thumbprint.");
            }

            return Profiler.Run(
                async () =>
                {
                    Device device = await iotHub.CreateEdgeDeviceIdentityAsync(deviceId, parentDeviceId, authType, x509Thumbprint, token);
                    return new EdgeDevice(device, true, iotHub);
                },
                "Created edge device '{Device}' on hub '{IotHub}'",
                deviceId,
                iotHub.Hostname);
        }

        public static async Task<Option<EdgeDevice>> GetIdentityAsync(
            string deviceId,
            Option<string> parentDeviceId,
            IotHub iotHub,
            CancellationToken token,
            bool takeOwnership = false)
        {
            IotHubDevice device = await iotHub.GetNestedDeviceIdentityAsync(deviceId, token);

            if (device != null)
            {
                await parentDeviceId.ForEachAsync(async p =>
                {
                    Device parentDevice = await iotHub.GetDeviceIdentityAsync(p, token);
                    device.ParentScopes = new[] { parentDevice.Scope };
                });

                if (!device.Capabilities.IotEdge)
                {
                    throw new InvalidOperationException($"Device '{device.Id}' exists, but is not an edge device");
                }

                return Option.Some(new EdgeDevice(device, takeOwnership, iotHub));
            }
            else
            {
                return Option.None<EdgeDevice>();
            }
        }

        public static async Task<EdgeDevice> GetOrCreateIdentityAsync(
            string deviceId,
            Option<string> parentDeviceId,
            IotHub iotHub,
            AuthenticationType authType,
            X509Thumbprint x509Thumbprint,
            CancellationToken token)
        {
            Option<EdgeDevice> device = await GetIdentityAsync(deviceId, parentDeviceId, iotHub, token);
            EdgeDevice edgeDevice = await device.Match(
                d =>
                {
                    Log.Information(
                        "Device '{Device}' already exists on hub '{IotHub}'",
                        d.Id,
                        iotHub.Hostname);
                    return Task.FromResult(d);
                },
                () => CreateIdentityAsync(deviceId, parentDeviceId, iotHub, authType, x509Thumbprint, token));
            return edgeDevice;
        }

        public Task DeleteIdentityAsync(CancellationToken token) =>
            Profiler.Run(
                () => this.iotHub.DeleteDeviceIdentityAsync(this.device, token),
                "Deleted edge device '{Device}'",
                this.Id);

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
