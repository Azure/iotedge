// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Linux
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class Platform : IPlatform
    {
        public async Task<string> CollectDaemonLogsAsync(DateTime testStartTime, string filePrefix, CancellationToken token)
        {
            string args = $"-u iotedge -u docker --since \"{testStartTime:yyyy-MM-dd HH:mm:ss}\" --no-pager";
            string[] output = await Process.RunAsync("journalctl", args, token);

            string daemonLog = $"{filePrefix}-iotedged.log";
            await File.WriteAllLinesAsync(daemonLog, output, token);

            return daemonLog;
        }

        public IEdgeDaemon CreateEdgeDaemon(Option<string> _) => new EdgeDaemon();

        public async Task<EdgeCertificates> GenerateEdgeCertificatesAsync(string deviceId, string scriptPath, CancellationToken token)
        {
            var command = $"'{scriptPath}' create_edge_device_certificate '{deviceId}'";

            await Profiler.Run(
                async () =>
                {
                    string[] output =
                        await Process.RunAsync("bash", $"-c \"{command}\"", token);
                    Log.Verbose(string.Join("\n", output));
                },
                "Created certificates for edge device");

            string dir = new FileInfo(scriptPath).DirectoryName;

            return new EdgeCertificates(
                $"{dir}/certs/iot-edge-device-{deviceId}-full-chain.cert.pem",
                $"{dir}/private/iot-edge-device-{deviceId}.key.pem",
                $"{dir}/certs/azure-iot-test-only.root.ca.cert.pem");
        }

        public async Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, string scriptPath, CancellationToken token)
        {
            var command = $"'{scriptPath}' create_device_certificate '{leafDeviceId}'";

            await Profiler.Run(
                async () =>
                {
                    string[] output =
                        await Process.RunAsync("bash", $"-c \"{command}\"", token);
                    Log.Verbose(string.Join("\n", output));
                },
                "Created certificates for leaf device");

            string dir = new FileInfo(scriptPath).DirectoryName;

            return new LeafCertificates(
                $"{dir}/certs/iot-device-{leafDeviceId}-full-chain.cert.pem",
                $"{dir}/private/iot-device-{leafDeviceId}.key.pem");
        }

        public StoreName GetCertificateStoreName() => StoreName.Root;

        public string GetConfigYamlPath() => "/etc/iotedge/config.yaml";

        public void InstallEdgeCertificates(IEnumerable<X509Certificate2> certs, ITransportSettings _) =>
            Common.Platform.InstallTrustedCertificates(certs, this.GetCertificateStoreName());

        public async Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token)
        {
            var command = $"'{scriptPath}' install_root_ca_from_files '{certPath}' '{keyPath}' '{password}'";
            string[] output = await Process.RunAsync("bash", $"-c \"{command}\"", token);
            Log.Verbose(string.Join("\n", output));
        }
    }
}
