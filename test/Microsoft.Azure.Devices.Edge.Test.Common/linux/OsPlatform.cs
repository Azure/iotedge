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

    public class OsPlatform : Common.OsPlatform, IOsPlatform
    {
        public async Task<string> CollectDaemonLogsAsync(DateTime testStartTime, string filePrefix, CancellationToken token)
        {
            string args = string.Join(
                " ",
                "-u aziot-keyd",
                "-u aziot-certd",
                "-u aziot-identityd",
                "-u aziot-edged",
                "-u docker",
                $"--since \"{testStartTime:yyyy-MM-dd HH:mm:ss}\"",
                "--no-pager");
            string[] output = await Process.RunAsync("journalctl", args, token);

            string daemonLog = $"{filePrefix}-daemon.log";
            await File.WriteAllLinesAsync(daemonLog, output, token);

            return daemonLog;
        }

        public async Task<IEdgeDaemon> CreateEdgeDaemonAsync(
            Option<string> _,
            CancellationToken token) => await EdgeDaemon.CreateAsync(token);

        public async Task<IdCertificates> GenerateIdentityCertificatesAsync(string deviceId, string scriptPath, CancellationToken token)
        {
            var command = BuildCertCommand($"create_device_certificate '{deviceId}'", scriptPath);
            await this.RunScriptAsync(("bash", command), token);
            return new IdCertificates(deviceId, scriptPath);
        }

        public async Task<CaCertificates> GenerateCaCertificatesAsync(string deviceId, string scriptPath, CancellationToken token)
        {
            var command = BuildCertCommand($"create_edge_device_certificate '{deviceId}'", scriptPath);
            await this.RunScriptAsync(("bash", command), token);
            return new CaCertificates(deviceId, scriptPath);
        }

        public void InstallCaCertificates(IEnumerable<X509Certificate2> certs, ITransportSettings transportSettings) =>
            this.InstallTrustedCertificates(certs, StoreName.Root);

        public Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token)
        {
            var command = BuildCertCommand($"install_root_ca_from_files '{certPath}' '{keyPath}' '{password}'", scriptPath);
            return this.InstallRootCertificateAsync(scriptPath, ("bash", command), token);
        }

        public void InstallTrustedCertificates(IEnumerable<X509Certificate2> certs) =>
            this.InstallTrustedCertificates(certs, StoreName.Root);

        static string BuildCertCommand(string command, string scriptPath) =>
            $"-c \"FORCE_NO_PROD_WARNING=true '{Path.Combine(scriptPath, "certGen.sh")}' {command}\"";

        public void SetOwner(string path, string owner, string permissions)
        {
            var chown = System.Diagnostics.Process.Start("chown", $"{owner}:{owner} {path}");
            chown.WaitForExit();
            chown.Close();

            var chmod = System.Diagnostics.Process.Start("chmod", $"{permissions} {path}");
            chmod.WaitForExit();
            chmod.Close();
        }

        public uint GetUid(string user)
        {
            var id = new System.Diagnostics.Process();

            id.StartInfo.FileName = "id";
            id.StartInfo.Arguments = $"-u {user}";
            id.StartInfo.RedirectStandardOutput = true;

            id.Start();
            StreamReader reader = id.StandardOutput;
            string uid = reader.ReadToEnd().Trim();

            id.WaitForExit();
            id.Close();

            return System.Convert.ToUInt32(uid, 10);
        }
    }
}
