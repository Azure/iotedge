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
        string hubConnectionString;

        public DeviceContext Context => this.context.Expect(
            () => new InvalidOperationException(
                $"No context for unknown device '{this.deviceId}'. Call " +
                "[GetOr]CreateAsync() first."
            )
        );

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
            DeviceContext context = _GetContext("Cannot delete");
            return this._DeleteIdentityAsync(context, token);
        }

        public async Task MaybeDeleteIdentityAsync(CancellationToken token)
        {
            DeviceContext context = _GetContext("Cannot delete");
            if (context.Owned)
            {
                await this._DeleteIdentityAsync(context, token);
            }
            else
            {
                Console.WriteLine($"Pre-existing device '{context.Device.Id}' was not deleted");
            }
        }

        Task _DeleteIdentityAsync(DeviceContext context, CancellationToken token)
        {
            return Profiler.Run(
                $"Deleting device '{context.Device.Id}'",
                () => context.Registry.RemoveDeviceAsync(context.Device)
            );
        }

        public Task UpdateModuleTwinAsync(string moduleId, object twinPatch, CancellationToken token)
        {
            DeviceContext context = _GetContext("Cannot update module twin for");
            return Profiler.Run(
                $"Updating twin for module '{moduleId}'",
                async () => {
                    Twin twin = await context.Registry.GetTwinAsync(context.Device.Id, moduleId, token);
                    string patch = JsonConvert.SerializeObject(twinPatch);
                    await context.Registry.UpdateTwinAsync(context.Device.Id, moduleId, patch, twin.ETag, token);
                }
            );
        }

        public Task WaitForTwinUpdatesAsync(string moduleId, object twinPatch, CancellationToken token)
        {
            DeviceContext context = _GetContext("Cannot get module twin updates for");
            return Profiler.Run(
                $"Waiting for expected twin updates for module '{moduleId}'",
                () => {
                    return Retry.Do(
                        async () => {
                            Twin twin = await context.Registry.GetTwinAsync(context.Device.Id, moduleId, token);
                            // TODO: Only newer versions of tempSensor mirror certain desired properties
                            //       to reported (e.g. 1.0.7-rc2). So return desired properties until
                            //       the temp-sensor e2e test can pull docker images other than the
                            //       default 'mcr...:1.0'.
                            // return twin.Properties.Reported;
                            return twin.Properties.Desired;
                        },
                        reported => {
                            JObject expected = JObject.FromObject(twinPatch)
                                .Value<JObject>("properties")
                                .Value<JObject>("reported");
                            return expected.Value<JObject>().All<KeyValuePair<string, JToken>>(
                                prop => reported.Contains(prop.Key) && reported[prop.Key] == prop.Value
                            );
                        },
                        null,
                        TimeSpan.FromSeconds(5),
                        token
                    );
                }
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