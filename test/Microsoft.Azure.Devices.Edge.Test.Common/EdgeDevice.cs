// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Net;
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

        readonly NestedEdgeConfig nestedEdgeConfig;

        public class NestedEdgeConfig
        {
            public string DeviceHostname { get; }
            public bool IsNestedEdge { get; }
            public Option<string> ParentDeviceId { get; }
            public string ParentHostname { get; }

            public NestedEdgeConfig(IotHub iotHub, bool isNestedEdge, Option<string> parentDeviceId, Option<string> parentHostname, Option<string> deviceHostname)
            {
                this.IsNestedEdge = isNestedEdge;

                // Since the Nested Edge Identity of the Edge Device is not managed by the E2E test framework,
                // the value needs be passed in from the pipeline. Thus, it cannot be empty.
                if (this.IsNestedEdge)
                {
                    parentDeviceId.Expect<ArgumentException>(() => throw new ArgumentException($"Expected {nameof(parentDeviceId)} for the Nested Edge"));
                }

                this.ParentDeviceId = parentDeviceId;

                this.ParentHostname = parentHostname.GetOrElse(iotHub.Hostname);

                this.DeviceHostname = deviceHostname.GetOrElse(Dns.GetHostName().ToLower());
            }
        }

        EdgeDevice(Device device, bool owned, IotHub iotHub, NestedEdgeConfig nestedEdgeConfig)
        {
            this.device = device;
            this.iotHub = iotHub;
            this.owned = owned;
            this.nestedEdgeConfig = nestedEdgeConfig;
        }

        public string HubHostname => this.iotHub.Hostname;
        public string Id => this.device.Id;
        public NestedEdgeConfig NestedEdge => this.nestedEdgeConfig;

        public string SharedAccessKey => this.device.Authentication.SymmetricKey.PrimaryKey;

        public static Task<EdgeDevice> CreateIdentityAsync(
            string deviceId,
            NestedEdgeConfig nestedEdgeConfig,
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
                    Device device = await iotHub.CreateEdgeDeviceIdentityAsync(deviceId, nestedEdgeConfig.ParentDeviceId, authType, x509Thumbprint, token);
                    return new EdgeDevice(device, true, iotHub, nestedEdgeConfig);
                },
                "Created edge device '{Device}' on hub '{IotHub}'",
                deviceId,
                iotHub.Hostname);
        }

        public static async Task<Option<EdgeDevice>> GetIdentityAsync(
            string deviceId,
            IotHub iotHub,
            CancellationToken token,
            bool takeOwnership = false,
            NestedEdgeConfig nestedEdgeConfig = default(NestedEdgeConfig))
        {
            Device device = await iotHub.GetDeviceIdentityAsync(deviceId, token);

            if (device != null)
            {
                if (nestedEdgeConfig != default(NestedEdgeConfig))
                {
                    await nestedEdgeConfig.ParentDeviceId.ForEachAsync(async p =>
                    {
                        Device parentDevice = await iotHub.GetDeviceIdentityAsync(p, token);
                        device.ParentScopes = new[] { parentDevice.Scope };
                    });
                }

                if (!device.Capabilities.IotEdge)
                {
                    throw new InvalidOperationException($"Device '{device.Id}' exists, but is not an edge device");
                }

                return Option.Some(new EdgeDevice(device, takeOwnership, iotHub, nestedEdgeConfig));
            }
            else
            {
                return Option.None<EdgeDevice>();
            }
        }

        public static async Task<EdgeDevice> GetOrCreateIdentityAsync(
            string deviceId,
            NestedEdgeConfig nestedEdgeConfig,
            IotHub iotHub,
            AuthenticationType authType,
            X509Thumbprint x509Thumbprint,
            CancellationToken token)
        {
            Option<EdgeDevice> device = await GetIdentityAsync(deviceId, iotHub, token, nestedEdgeConfig: nestedEdgeConfig);
            EdgeDevice edgeDevice = await device.Match(
                d =>
                {
                    Log.Information(
                        "Device '{Device}' already exists on hub '{IotHub}'",
                        d.Id,
                        iotHub.Hostname);
                    return Task.FromResult(d);
                },
                () => CreateIdentityAsync(deviceId, nestedEdgeConfig, iotHub, authType, x509Thumbprint, token));
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
