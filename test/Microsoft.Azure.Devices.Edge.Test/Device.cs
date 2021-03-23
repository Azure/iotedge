// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;
    using Serilog;

    [EndToEnd]
    class Device : SasManualProvisioningFixture
    {
        [Test]
        [Category("CentOsSafe")]
        public async Task QuickstartCerts()
        {
            CancellationToken token = this.TestToken;

            await this.runtime.DeployConfigurationAsync(token, Context.Current.NestedEdge);

            string leafDeviceId = DeviceId.Current.Generate();

            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                Protocol.Amqp,
                AuthenticationType.Sas,
                Option.Some(this.runtime.DeviceId),
                false,
                this.ca,
                this.IotHub,
                Context.Current.Hostname.GetOrElse(Dns.GetHostName().ToLower()),
                token,
                Option.None<string>(),
                Context.Current.NestedEdge);

            await TryFinally.DoAsync(
                async () =>
                {
                    DateTime seekTime = DateTime.Now;
                    await leaf.SendEventAsync(token);
                    await leaf.WaitForEventsReceivedAsync(seekTime, token);
                    await leaf.InvokeDirectMethodAsync(token);
                },
                async () =>
                {
                    await leaf.DeleteIdentityAsync(token);
                });
        }

        [Test]
        [Category("CentOsSafe")]
        [Category("NestedEdgeOnly")]
        public async Task QuickstartChangeSasKey()
        {
            CancellationToken token = this.TestToken;

            await this.runtime.DeployConfigurationAsync(token, Context.Current.NestedEdge);

            string leafDeviceId = DeviceId.Current.Generate();

            // Create leaf and send message
            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                Protocol.Amqp,
                AuthenticationType.Sas,
                Option.Some(this.runtime.DeviceId),
                false,
                this.ca,
                this.IotHub,
                Context.Current.Hostname.GetOrElse(Dns.GetHostName().ToLower()),
                token,
                Option.None<string>(),
                Context.Current.NestedEdge);

            await TryFinally.DoAsync(
                async () =>
                {
                    DateTime seekTime = DateTime.Now;
                    await leaf.SendEventAsync(token);
                    await leaf.WaitForEventsReceivedAsync(seekTime, token);
                    await leaf.InvokeDirectMethodAsync(token);
                },
                async () =>
                {
                    await leaf.Close();
                    await leaf.DeleteIdentityAsync(token);
                });

            // Re-create the leaf with the same device ID, for our purposes this is
            // the equivalent of updating the SAS keys
            var leafUpdated = await LeafDevice.CreateAsync(
                leafDeviceId,
                Protocol.Amqp,
                AuthenticationType.Sas,
                Option.Some(this.runtime.DeviceId),
                false,
                this.ca,
                this.IotHub,
                Context.Current.Hostname.GetOrElse(Dns.GetHostName().ToLower()),
                token,
                Option.None<string>(),
                Context.Current.NestedEdge);

            await TryFinally.DoAsync(
                async () =>
                {
                    DateTime seekTime = DateTime.Now;
                    await leafUpdated.SendEventAsync(token);
                    await leafUpdated.WaitForEventsReceivedAsync(seekTime, token);
                    await leafUpdated.InvokeDirectMethodAsync(token);
                },
                async () =>
                {
                    await leafUpdated.Close();
                    await leafUpdated.DeleteIdentityAsync(token);
                });
        }

        [Test]
        [Category("CentOsSafe")]
        [Category("NestedEdgeOnly")]
        [Category("NestedEdgeAmqpOnly")]
        public async Task RouteMessageL3LeafToL4Module()
        {
            CancellationToken token = this.TestToken;

            await this.runtime.DeployConfigurationAsync(token, Context.Current.NestedEdge);

            // These must match the IDs in nestededge_middleLayerBaseDeployment_amqp.json,
            // which defines a route that filters by deviceId to forwards the message
            // to the relayer module
            string parentDeviceId = Context.Current.ParentDeviceId.Expect(() => new InvalidOperationException("No parent device ID set!"));
            string leafDeviceId = "L3LeafToRelayer1";
            string relayerModuleId = "relayer1";

            // Create leaf and send message
            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                Protocol.Amqp,
                AuthenticationType.Sas,
                Option.Some(this.runtime.DeviceId),
                false,
                this.ca,
                this.IotHub,
                Context.Current.Hostname.GetOrElse(Dns.GetHostName().ToLower()),
                token,
                Option.None<string>(),
                Context.Current.NestedEdge);

            await TryFinally.DoAsync(
                async () =>
                {
                    // Send a message from the leaf device
                    DateTime seekTime = DateTime.Now;
                    await leaf.SendEventAsync(token);
                    Log.Information($"Sent message from {leafDeviceId}");

                    // Verify that the message was received/resent by the relayer module on L4
                    await Profiler.Run(
                        () => this.IotHub.ReceiveEventsAsync(
                            parentDeviceId,
                            seekTime,
                            data =>
                            {
                                data.SystemProperties.TryGetValue("iothub-connection-device-id", out object devId);
                                data.SystemProperties.TryGetValue("iothub-connection-module-id", out object modId);
                                data.Properties.TryGetValue("leaf-message-id", out object msgId);

                                Log.Verbose($"Received event for '{devId + "/" + modId}' with message ID '{msgId}'");

                                return devId != null && devId.ToString().Equals(parentDeviceId)
                                                     && modId.ToString().Equals(relayerModuleId);
                            },
                            token),
                        "Received events from module '{Device}' on Event Hub '{EventHub}'",
                        parentDeviceId + "/" + relayerModuleId,
                        this.IotHub.EntityPath);
                },
                async () =>
                {
                    await leaf.Close();
                    await leaf.DeleteIdentityAsync(token);
                });
        }

        [Test]
        [Category("CentOsSafe")]
        public async Task DisableReenableParentEdge()
        {
            CancellationToken token = this.TestToken;

            Log.Information("Deploying L3 Edge");
            await this.runtime.DeployConfigurationAsync(token, Context.Current.NestedEdge);

            // Disable the parent Edge device
            Log.Information("Disabling Edge device");
            await this.IotHub.UpdateEdgeEnableStatus(this.runtime.DeviceId, false);
            await Task.Delay(TimeSpan.FromSeconds(10));

            // Re-enable parent Edge
            Log.Information("Re-enabling Edge device");
            await this.IotHub.UpdateEdgeEnableStatus(this.runtime.DeviceId, true);
            await Task.Delay(TimeSpan.FromSeconds(10));

            // Try connecting
            string leafDeviceId = DeviceId.Current.Generate();
            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                Protocol.Amqp,
                AuthenticationType.Sas,
                Option.Some(this.runtime.DeviceId),
                false,
                this.ca,
                this.IotHub,
                Context.Current.Hostname.GetOrElse(Dns.GetHostName().ToLower()),
                token,
                Option.None<string>(),
                Context.Current.NestedEdge);

            await TryFinally.DoAsync(
                async () =>
                {
                    DateTime seekTime = DateTime.Now;
                    await leaf.SendEventAsync(token);
                    await leaf.WaitForEventsReceivedAsync(seekTime, token);
                    await leaf.InvokeDirectMethodAsync(token);
                },
                async () =>
                {
                    await leaf.DeleteIdentityAsync(token);
                });
        }
    }
}
