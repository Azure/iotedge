// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using NUnit.Framework;
    using Serilog;

    [EndToEnd]
    class IoTEdgeCheck : SasManualProvisioningFixture
    {
        [Test]
        [Category("CentOsSafe")]
        public async Task IotEdgeCheck()
        {
            CancellationToken token = this.TestToken;
            // Need to deploy edgeHub or one check will fail
            await this.runtime.DeployConfigurationAsync(this.cli, token, Context.Current.NestedEdge);

            string diagnosticImageName = Context.Current
                .DiagnosticsImage.Expect(() => new ArgumentException("Missing diagnostic image"));

            string args = $"check --diagnostics-image-name {diagnosticImageName} --verbose";
            if (Context.Current.EdgeProxy.HasValue)
            {
                // When test runs behind proxy, skip MQTT and AMQP checks which legitimately fail
                args += string.Join(" ", new[]
                {
                    " --dont-run",
                    "container-connect-upstream-mqtt",
                    "container-connect-upstream-amqp",
                    "container-default-connect-upstream-mqtt",
                    "container-default-connect-upstream-amqp",
                    "host-connect-iothub-mqtt",
                    "host-connect-iothub-amqp"
                });
            }

            // NTP Server Sync Test intermittently fails on ARM. Disable that check for now.
            else if (Context.Current.EdgeAgentImage.GetOrElse(string.Empty).Contains("arm"))
            {
                args += string.Join(" ", new[]
                {
                    " --dont-run",
                    "container-local-time",
                });
            }

            string errors_number = string.Empty;

            void OnStdout(string o)
            {
                Log.Verbose(o);
                if (o.Contains("check(s) raised errors"))
                {
                    // Extract the number of errors
                    errors_number = o;
                }
            }

            void OnStderr(string e) => Log.Verbose(e);

            await this.cli.RunAsync(args, OnStdout, OnStderr, token);

            Assert.AreEqual(string.Empty, errors_number);
        }
    }
}
