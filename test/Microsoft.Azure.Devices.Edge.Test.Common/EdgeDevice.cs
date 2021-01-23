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
        readonly string parentDeviceId;
        readonly bool owned;

        // Note: We only support a single parent device at the moment (1/22/2021)
        // This assumption may change down the road and the constructor will need to be updated.
        EdgeDevice(Device device, bool owned, IotHub iotHub, string parentDeviceId)
        {
            this.device = device;
            this.iotHub = iotHub;
            this.owned = owned;
            this.parentDeviceId = parentDeviceId;
        }

        public string HubHostname => this.iotHub.Hostname;
        public string Id => this.device.Id;
        public string ParentDeviceId => this.parentDeviceId;
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
                    return new EdgeDevice(device, true, iotHub, parentDeviceId.GetOrElse(string.Empty));
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
            Device device = await iotHub.GetDeviceIdentityAsync(deviceId, token);

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

                return Option.Some(new EdgeDevice(device, takeOwnership, iotHub, parentDeviceId.GetOrElse(string.Empty)));
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
            // BEARWASHERE -- Need to keep track of parentDeviceId -- DONE
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
                // BEARWASHERE -- Need to keep track of parentDeviceId  -- DONE
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
