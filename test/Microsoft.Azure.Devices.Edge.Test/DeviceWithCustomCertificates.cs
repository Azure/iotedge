// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;

    [EndToEnd]
    class DeviceWithCustomCertificates : CustomCertificatesFixture
    {
        [Test]
        [Category("Flaky")]
        public async Task TransparentGateway(
            [Values] TestAuthenticationType testAuth,
            [Values(Protocol.Mqtt, Protocol.Amqp)] Protocol protocol)
        {
            CancellationToken token = this.TestToken;

            await this.runtime.DeployConfigurationAsync(this.cli, token, this.device.NestedEdge.IsNestedEdge);

            string leafDeviceId = DeviceId.Current.Generate();

            Option<string> parentId = testAuth == TestAuthenticationType.SasOutOfScope
                ? Option.None<string>()
                : Option.Some(this.runtime.DeviceId);

            if (!parentId.HasValue)
            {
                Assert.Fail("Expected to fail when not in scope.");
            }

            using var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                protocol,
                testAuth.ToAuthenticationType(),
                parentId,
                testAuth.UseSecondaryCertificate(),
                this.ca,
                this.daemon.GetCertificatesPath(),
                this.IotHub,
                this.device.NestedEdge.DeviceHostname,
                token,
                Option.None<string>(),
                this.device.NestedEdge.IsNestedEdge);

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
                    await leaf.CloseAsync();
                    await leaf.DeleteIdentityAsync(token);
                });
        }

        [Test]
        [Category("NestedEdgeOnly")]
        [Category("FlakyOnNested")]
        [Description("A test to verify a leaf device can be registered under grandparent device scope.")]
        public async Task GrandparentScopeDevice(
            [Values(
                TestAuthenticationType.SasInScope,
                TestAuthenticationType.SelfSignedPrimary,
                TestAuthenticationType.SelfSignedSecondary)] TestAuthenticationType testAuth,
            [Values(Protocol.Mqtt, Protocol.Amqp)] Protocol protocol)
        {
            if (!this.device.NestedEdge.IsNestedEdge)
            {
                Assert.Ignore("The test can only be run in the nested edge topology");
            }

            Option<string> parentId = Option.Some(this.runtime.DeviceId);
            if (!parentId.HasValue)
            {
                Assert.Fail("Expected to fail when not in scope.");
            }

            CancellationToken token = this.TestToken;

            await this.runtime.DeployConfigurationAsync(this.cli, token, Context.Current.NestedEdge);

            string leafDeviceId = DeviceId.Current.Generate();

            using var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                protocol,
                testAuth.ToAuthenticationType(),
                parentId,
                testAuth.UseSecondaryCertificate(),
                this.ca,
                this.daemon.GetCertificatesPath(),
                this.IotHub,
                this.device.NestedEdge.ParentHostname,
                token,
                Option.None<string>(),
                this.device.NestedEdge.IsNestedEdge);

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
                    await leaf.CloseAsync();
                    await leaf.DeleteIdentityAsync(token);
                });
        }
    }
}
