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

    class Device : DeviceBase
    {
        [Test]
        public async Task TransparentGateway(
            [Values] TestAuthenticationType testAuth,
            [Values(Protocol.Mqtt, Protocol.Amqp)] Protocol protocol)
        {
            // For CA and self-signed cert tests, temporarily disable AMQP
            var auth = testAuth.ToAuthenticationType();
            if (protocol == Protocol.Amqp &&
                (auth == AuthenticationType.CertificateAuthority || auth == AuthenticationType.SelfSigned))
            {
                Assert.Ignore("x509 cert + AMQP tests disabled until bug is resolved");
            }

            CancellationToken token = this.cts.Token;

            // Generate a leaf device ID--based on the (edge) device ID--that is at most
            // (deviceId.Length + 26 chars) long. This gives us a leaf device ID of <= 63
            // characters, and gives LeafDevice.CreateAsync (called below) some wiggle room to
            // create certs with unique CNs that don't exceed the 64-char limit.
            string leafDeviceId = $"{Context.Current.DeviceId}-{protocol.ToString()}-{testAuth.ToString()}";

            Option<string> parentId = testAuth == TestAuthenticationType.SasOutOfScope
                ? Option.None<string>()
                : Option.Some(Context.Current.DeviceId);

            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                protocol,
                auth,
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
