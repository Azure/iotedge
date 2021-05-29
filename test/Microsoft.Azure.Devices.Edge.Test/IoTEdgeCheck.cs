// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;
    using Serilog;

    [EndToEnd]
    class IoTEdgeCheck : SasManualProvisioningFixture
    {
        [Test]
        [Category("CentOsSafe")]
        public async Task IoTEdge_check()
        {
            CancellationToken token = this.TestToken;
            // Need to deploy edgeHub or one check will fail
            await this.runtime.DeployConfigurationAsync(token, Context.Current.NestedEdge);

            string diagnosticImageName = Context.Current
                .DiagnosticsImage.Expect(() => new ArgumentException("Missing diagnostic image"));

            // If we are in a nested configuration, we don't need to login to docker because the bottom layer
            // will ask the upper layer for the image through ApiProxy, which will continue up the chain
            // until the top layer, which has permissions, can get it, and send it back down.
            // Non-nested configuration needs it because we are calling out to a new process, and this process
            // needs auth
            // We can also skip if the user has provided a public image from mcr.microsoft.com that requires no auth
            if (!Context.Current.NestedEdge || diagnosticImageName.Contains("mcr.microsoft.com"))
            {
                Registry registry = Context.Current.Registries.First();
                foreach (var reg in Context.Current.Registries)
                {
                    if (diagnosticImageName.Contains(reg.Address))
                    {
                        // Get the registry that corresponds to the diagnosticImageName in case there are multiple registries.
                        registry = reg;
                    }
                }

                await Process.RunAsync(
                    "docker",
                    $"login --username {registry.Username} --password {registry.Password} {registry.Address}",
                    token);
            }

            string args = $"check --diagnostics-image-name {diagnosticImageName} --verbose";
            if (Context.Current.EdgeProxy.HasValue)
            {
                args += string.Join(" ", new[]
                {
                    "--dont-run",
                    "container-connect-upstream-mqtt",
                    "container-connect-upstream-amqp",
                    "container-default-connect-upstream-mqtt",
                    "container-default-connect-upstream-amqp",
                    "host-connect-iothub-mqtt",
                    "host-connect-iothub-amqp"
                });
            }

            string errors_number = string.Empty;
            Action<string> onStdout = (string o) =>
            {
                Log.Verbose(o);
                if (o.Contains("check(s) raised errors"))
                {
                    // Extract the number of errors
                    errors_number = o;
                }
            };
            Action<string> onStderr = (string e) => Log.Verbose(e);

            await Process.RunAsync("iotedge", args, onStdout, onStderr, token);

            Assert.AreEqual(string.Empty, errors_number);
        }
    }
}
