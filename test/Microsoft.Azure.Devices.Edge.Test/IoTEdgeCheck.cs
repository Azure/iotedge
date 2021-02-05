// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Diagnostics;
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
                Registry diagnosticsRegistry = Context.Current.Registries.First();
                foreach (var registry in Context.Current.Registries)
                {
                    if (diagnosticImageName.Contains(registry.Address))
                    {
                        // Get the registry that corresponds to the diagnosticImageName in case there are multiple registries.
                        diagnosticsRegistry = registry;
                    }
                }

                var dockerLoginProcess = new System.Diagnostics.Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sudo",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        ArgumentList =
                        {
                            "docker",
                            "login",
                            "--username",
                            diagnosticsRegistry.Username,
                            "--password",
                            diagnosticsRegistry.Password,
                            diagnosticsRegistry.Address
                        }
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

            // TODO: iotedge check currently has a legitimate error in it. When we update the aziot-submodule, the error
            // will go away, and we should change this assertion to empty string instead of passing on an errors.
            // This is better than disabling the test, because it still tests iotedge check to some extent, and it will
            // remind us with a failed run to update this test.
            if (await this.IsCentos(token))
            {
                // CentOS actually has 2 errors
                Assert.AreEqual("2 check(s) raised errors.", errors_number);
            }
            else
            {
                Assert.AreEqual("1 check(s) raised errors.", errors_number);
            }
        }

        private async Task<bool> IsCentos(CancellationToken token)
        {
            string[] platformInfo = await Microsoft.Azure.Devices.Edge.Test.Common.Process.RunAsync("lsb_release", "-sir", token);
            if (platformInfo.Length == 1)
            {
                platformInfo = platformInfo[0].Split(' ');
            }

            string os = platformInfo[0].Trim();
            switch (os)
            {
                case "CentOS":
                    return true;
                default:
                    return false;
            }
        }
    }
}
