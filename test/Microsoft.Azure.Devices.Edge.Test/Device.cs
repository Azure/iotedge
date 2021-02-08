// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;

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
                this.iotHub,
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
                this.iotHub,
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
                this.iotHub,
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
    }
}
