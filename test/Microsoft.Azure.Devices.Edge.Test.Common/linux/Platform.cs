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
            var command = BuildCertCommand($"create_edge_device_certificate '{deviceId}'", scriptPath);
            return Common.Platform.GenerateEdgeCertificatesAsync(deviceId, scriptPath, ("bash", command), token);
        }

        public Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, string scriptPath, CancellationToken token)
        {
            var command = BuildCertCommand($"create_device_certificate '{leafDeviceId}'", scriptPath);
            return Common.Platform.GenerateLeafCertificatesAsync(leafDeviceId, scriptPath, ("bash", command), token);
        }

        public StoreName GetCertificateStoreName() => StoreName.Root;

        public void InstallEdgeCertificates(IEnumerable<X509Certificate2> certs, ITransportSettings _) =>
            Common.Platform.InstallTrustedCertificates(certs, this.GetCertificateStoreName());

        public Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token)
        {
            var command = BuildCertCommand($"install_root_ca_from_files '{certPath}' '{keyPath}' '{password}'", scriptPath);
            return Common.Platform.InstallRootCertificateAsync(scriptPath, ("bash", command), token);
        }

        static string BuildCertCommand(string command, string scriptPath) =>
            $"-c \"'{Path.Combine(scriptPath, "certGen.sh")}' {command}\"";
    }
}
