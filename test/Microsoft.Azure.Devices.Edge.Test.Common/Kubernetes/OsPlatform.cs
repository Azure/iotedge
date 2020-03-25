// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Kubernetes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;

    public class OsPlatform : Common.OsPlatform, IOsPlatform
    {
        public bool IsKubernetes => true;

        // Probably need to add a "skip test for feature" to interface.
        public async Task<string> CollectDaemonLogsAsync(DateTime testStartTime, string filePrefix, CancellationToken token)
        {
            // set up kubectl so all commands execute in device namespace
            // Find iotedged container in namespace - we should set up current context with namespace
            // k get po --output=jsonpath="{.items[*].metadata.name}"
            // Split on whitespace, find first of pod that starts with iotedged.
            // get logs from container.
            string podname = (await KubeUtils.FindPod("iotedged", token)).OrDefault();

            string args = $"logs -n {Constants.Deployment} {podname} --since-time=\"{testStartTime:yyyy-MM-dd HH:mm:ss}\"";
            string[] output = await Process.RunAsync("kubectl", args, token);

            string daemonLog = $"{filePrefix}-iotedged.log";
            await File.WriteAllLinesAsync(daemonLog, output, token);

            return daemonLog;
        }

        public IEdgeDaemon CreateEdgeDaemon(Option<string> _) => new EdgeDaemon();

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

        // Need to think about this one - can probably copy out of iotedged container.
        public CaCertificates GetEdgeQuickstartCertificates() =>
            this.GetEdgeQuickstartCertificates("/var/lib/iotedge/hsm");

        public void InstallCaCertificates(IEnumerable<X509Certificate2> certs, ITransportSettings transportSettings)
        {
            // maybe install this as a secret in the namespace.
            // Anything else?
            if (transportSettings.GetTransportType() == TransportType.Amqp_WebSocket_Only ||
                transportSettings.GetTransportType() == TransportType.Amqp_Tcp_Only)
            {
                transportSettings.SetupCertificateValidation(certs.First());
            }
            else
            {
                this.InstallTrustedCertificates(certs, StoreName.Root);
            }
        }

        public Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token)
        {
            var command = BuildCertCommand($"install_root_ca_from_files '{certPath}' '{keyPath}' '{password}'", scriptPath);
            return this.InstallRootCertificateAsync(scriptPath, ("bash", command), token);
        }

        public void InstallTrustedCertificates(IEnumerable<X509Certificate2> certs) =>
            this.InstallTrustedCertificates(certs, StoreName.Root);

        static string BuildCertCommand(string command, string scriptPath) =>
            $"-c \"FORCE_NO_PROD_WARNING=true '{Path.Combine(scriptPath, "certGen.sh")}' {command}\"";
    }
}
