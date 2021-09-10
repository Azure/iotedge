// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;

    [EndToEnd]
    class X509Device : X509ManualProvisioningFixture
    {
        [Test]
        public async Task X509ManualProvision()
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
    }
}
