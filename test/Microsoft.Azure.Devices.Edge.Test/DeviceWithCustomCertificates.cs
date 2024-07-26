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

    [TestClass, TestCategory("EndToEnd")]
    public class DeviceWithCustomCertificates : CustomCertificatesFixture
    {
        [TestMethod, TestCategory("Flaky")]
        [DataRow(TestAuthenticationType.SasInScope, Protocol.Mqtt)]
        [DataRow(TestAuthenticationType.SasOutOfScope, Protocol.Mqtt)]
        [DataRow(TestAuthenticationType.CertificateAuthority, Protocol.Mqtt)]
        [DataRow(TestAuthenticationType.SelfSignedPrimary, Protocol.Mqtt)]
        [DataRow(TestAuthenticationType.SelfSignedSecondary, Protocol.Mqtt)]
        [DataRow(TestAuthenticationType.SasInScope, Protocol.Amqp)]
        [DataRow(TestAuthenticationType.SasOutOfScope, Protocol.Amqp)]
        [DataRow(TestAuthenticationType.CertificateAuthority, Protocol.Amqp)]
        [DataRow(TestAuthenticationType.SelfSignedPrimary, Protocol.Amqp)]
        [DataRow(TestAuthenticationType.SelfSignedSecondary, Protocol.Amqp)]
        public async Task TransparentGateway(TestAuthenticationType testAuth, Protocol protocol)
        {
            CancellationToken token = TestToken;

            await runtime.DeployConfigurationAsync(cli, token, device.NestedEdge.IsNestedEdge);

            string leafDeviceId = DeviceId.Current.Generate();

            Option<string> parentId = testAuth == TestAuthenticationType.SasOutOfScope
                ? Option.None<string>()
                : Option.Some(runtime.DeviceId);

            LeafDevice leaf = null;
            try
            {
                leaf = await LeafDevice.CreateAsync(
                    leafDeviceId,
                    protocol,
                    testAuth.ToAuthenticationType(),
                    parentId,
                    testAuth.UseSecondaryCertificate(),
                    ca,
                    daemon.GetCertificatesPath(),
                    IotHub,
                    device.NestedEdge.DeviceHostname,
                    token,
                    Option.None<string>(),
                    device.NestedEdge.IsNestedEdge);
            }
            catch (Exception) when (!parentId.HasValue)
            {
                return;
            }

            if (!parentId.HasValue)
            {
                Assert.Fail("Expected to fail when not in scope.");
            }

            Assert.IsNotNull(leaf);

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

        [TestMethod, TestCategory("NestedEdgeOnly"), TestCategory("FlakyOnNested")]
        [Description("A test to verify a leaf device can be registered under grandparent device scope.")]
        [DataRow(TestAuthenticationType.SasInScope, Protocol.Mqtt)]
        [DataRow(TestAuthenticationType.SelfSignedPrimary, Protocol.Mqtt)]
        [DataRow(TestAuthenticationType.SelfSignedSecondary, Protocol.Mqtt)]
        [DataRow(TestAuthenticationType.SasInScope, Protocol.Amqp)]
        [DataRow(TestAuthenticationType.SelfSignedPrimary, Protocol.Amqp)]
        [DataRow(TestAuthenticationType.SelfSignedSecondary, Protocol.Amqp)]
        public async Task GrandparentScopeDevice(TestAuthenticationType testAuth, Protocol protocol)
        {
            if (!device.NestedEdge.IsNestedEdge)
            {
                Assert.Inconclusive("The test can only be run in the nested edge topology");
            }

            Option<string> parentId = Option.Some(runtime.DeviceId);

            CancellationToken token = TestToken;

            await runtime.DeployConfigurationAsync(cli, token, Context.Current.NestedEdge);

            string leafDeviceId = DeviceId.Current.Generate();

            LeafDevice leaf = null;
            try
            {
                leaf = await LeafDevice.CreateAsync(
                    leafDeviceId,
                    protocol,
                    testAuth.ToAuthenticationType(),
                    parentId,
                    testAuth.UseSecondaryCertificate(),
                    ca,
                    daemon.GetCertificatesPath(),
                    IotHub,
                    device.NestedEdge.ParentHostname,
                    token,
                    Option.None<string>(),
                    device.NestedEdge.IsNestedEdge);
            }
            catch (Exception) when (!parentId.HasValue)
            {
                return;
            }

            if (!parentId.HasValue)
            {
                Assert.Fail("Expected to fail when not in scope.");
            }

            Assert.IsNotNull(leaf);

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
                    await Task.CompletedTask;
                });
        }
    }
}
