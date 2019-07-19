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

        public Task<EdgeCertificates> GenerateEdgeCertificatesAsync(string deviceId, string scriptPath, CancellationToken token)
        {
            var command = $"'{scriptPath}' create_edge_device_certificate '{deviceId}'";

            return Common.Platform.GenerateEdgeCertificatesAsync(
                deviceId,
                scriptPath,
                ("bash", $"-c \"{command}\""),
                token);
        }

        public Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, string scriptPath, CancellationToken token)
        {
            var command = $"'{scriptPath}' create_device_certificate '{leafDeviceId}'";

            return Common.Platform.GenerateLeafCertificatesAsync(
                leafDeviceId,
                scriptPath,
                ("bash", $"-c \"{command}\""),
                token);
        }

        public StoreName GetCertificateStoreName() => StoreName.Root;

        public string GetConfigYamlPath() => "/etc/iotedge/config.yaml";

        public void InstallEdgeCertificates(IEnumerable<X509Certificate2> certs, ITransportSettings _) =>
            Common.Platform.InstallTrustedCertificates(certs, this.GetCertificateStoreName());

        public Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token)
        {
            var command = $"'{scriptPath}' install_root_ca_from_files '{certPath}' '{keyPath}' '{password}'";

            return Common.Platform.InstallRootCertificateAsync(
                scriptPath,
                ("bash", $"-c \"{command}\""),
                token);
        }
    }
}
