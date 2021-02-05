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
            // Need to deploy edgeHu or one check will fail
            await this.runtime.DeployConfigurationAsync(token, Context.Current.NestedEdge);

            string diagnosticImageName = Context.Current
                .DiagnosticsImage.Expect(() => new ArgumentException("Missing diagnostic image"));

            var process1 = new System.Diagnostics.Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    ArgumentList = { "docker", "pull", diagnosticImageName, "--username", Context.Current.Registries.First().Username, "--password", Context.Current.Registries.First().Password }
                }
            };
            process1.Start();

            var process = new System.Diagnostics.Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    ArgumentList = { "iotedge", "check", "--diagnostics-image-name", diagnosticImageName, "--verbose" }
                }
            };
            string errors_number = string.Empty;
            process.Start();
            await Task.Run(() =>
            {
                while (!process.StandardOutput.EndOfStream)
                {
                    string line = process.StandardOutput.ReadLine();
                    Log.Information(line);
                    if (line.Contains("check(s) raised errors"))
                    {
                        // Extract the number of errors
                        errors_number = line;
                    }
                }
                while (!process.StandardError.EndOfStream)
                {
                    string line = process.StandardError.ReadLine();
                    Log.Information(line);
                }
            });

            Assert.AreEqual(string.Empty, errors_number);
        }
    }
}
