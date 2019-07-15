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

            string leafDeviceId = $"{Context.Current.DeviceId}-{protocol.ToString()}-{testAuth.ToString()}"; // at most (deviceId.Length + 26 chars)
            Option<string> parentId = testAuth == TestAuthenticationType.SasOutOfScope
                ? Option.None<string>()
                : Option.Some(Context.Current.DeviceId);

            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                protocol,
                auth,
                parentId,
                testAuth.UseSecondaryCertificate(),
                this.edgeCa,
                this.iotHub,
                token);

            try
            {
                DateTime seekTime = DateTime.Now;
                await leaf.SendEventAsync(token);
                await leaf.WaitForEventsReceivedAsync(seekTime, token);
                await leaf.InvokeDirectMethodAsync(token);
            }

            // According to C# reference docs for 'try-finally', the finally
            // block may or may not run for unhandled exceptions. The
            // workaround is to catch the exception here and rethrow,
            // guaranteeing that the finally block will run.
            // ReSharper disable once RedundantCatchClause
            catch
            {
                throw;
            }
            finally
            {
                await leaf.DeleteIdentityAsync(token);
            }
        }
    }
}
