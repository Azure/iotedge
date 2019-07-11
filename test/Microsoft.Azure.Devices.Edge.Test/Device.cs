// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;
    using Serilog;

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

            string name = $"transparent gateway ({testAuth.ToString()}, {protocol.ToString()})";
            Log.Information("Running test '{Name}'", name);

            await Profiler.Run(
                async () =>
                {
                    string leafDeviceId = $"{Context.Current.DeviceId}-{protocol.ToString()}-{testAuth.ToString()}-leaf";
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
                        await leaf.SendEventAsync(token);
                        await leaf.WaitForEventsReceivedAsync(token);
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
                },
                "Completed test '{Name}'",
                name);
        }
    }
}
