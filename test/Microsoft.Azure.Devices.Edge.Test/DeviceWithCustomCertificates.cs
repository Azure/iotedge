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

    class DeviceWithCustomCertificates : CustomCertificatesFixture
    {
        [Test]
        public async Task TransparentGateway(
            [Values] TestAuthenticationType testAuth,
            [Values(Protocol.Mqtt, Protocol.Amqp)] Protocol protocol)
        {
            if (!OsPlatform.IsWindows() && (protocol == Protocol.Amqp))
            {
                switch (testAuth)
                {
                    case TestAuthenticationType.SelfSignedPrimary:
                    case TestAuthenticationType.SelfSignedSecondary:
                    case TestAuthenticationType.CertificateAuthority:
                        Assert.Ignore("Skipping the test case due to BUG 5234369");
                        break;

                    default:
                        // Intentionally left blank
                        break;
                }
            }

            CancellationToken token = this.TestToken;

            string leafDeviceId = IdentityLimits.CheckLeafId(
                $"{Context.Current.DeviceId}-{protocol}-{testAuth}");

            Option<string> parentId = testAuth == TestAuthenticationType.SasOutOfScope
                ? Option.None<string>()
                : Option.Some(Context.Current.DeviceId.ToString());

            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                protocol,
                testAuth.ToAuthenticationType(),
                parentId,
                testAuth.UseSecondaryCertificate(),
                this.ca,
                this.iotHub,
                token);

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
