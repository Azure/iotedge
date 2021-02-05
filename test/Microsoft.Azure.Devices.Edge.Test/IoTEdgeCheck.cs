// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
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
            if (!Context.Current.NestedEdge)
            {
                var dockerLoginProcess = new System.Diagnostics.Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sudo",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        ArgumentList = { "docker", "login", "--username", Context.Current.Registries.First().Username, "--password", Context.Current.Registries.First().Password, Context.Current.Registries.First().Address }
                    }
                };
                dockerLoginProcess.Start();
                await Task.Run(() =>
                {
                    while (!dockerLoginProcess.StandardOutput.EndOfStream)
                    {
                        string line = dockerLoginProcess.StandardOutput.ReadLine();
                        Log.Information(line);
                    }
                });
            }

            var iotedgeCheckProcess = new System.Diagnostics.Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    ArgumentList = { "iotedge", "check", "--diagnostics-image-name", diagnosticImageName, "--verbose" }
                }
            };
            string errors_number = string.Empty;
            iotedgeCheckProcess.Start();
            await Task.Run(() =>
            {
                while (!iotedgeCheckProcess.StandardOutput.EndOfStream)
                {
                    string line = iotedgeCheckProcess.StandardOutput.ReadLine();
                    Log.Information(line);
                    if (line.Contains("check(s) raised errors"))
                    {
                        // Extract the number of errors
                        errors_number = line;
                    }
                }
            });

            Assert.AreEqual(string.Empty, errors_number);
        }
    }
}
