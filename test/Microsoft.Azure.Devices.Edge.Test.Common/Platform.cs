// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;

    public static class Platform
    {
        public static Task<string> CollectDaemonLogsAsync(DateTime testStartTime, string filePrefix, CancellationToken token) => IsWindows()
            ? Windows.EdgeDaemon.CollectLogsAsync(testStartTime, filePrefix, token)
            : Linux.EdgeDaemon.CollectLogsAsync(testStartTime, filePrefix, token);

        // TODO: download installer script from aka.ms if user doesn't pass installerPath in Windows
        public static IEdgeDaemon CreateEdgeDaemon(Option<string> installerPath) => IsWindows()
            ? new Windows.EdgeDaemon(installerPath.Expect(() => new ArgumentException()))
            : new Linux.EdgeDaemon() as IEdgeDaemon;

        // After calling this function, the following files will be available under {scriptPath}:
        //  certs/iot-edge-device-{deviceId}-full-chain.cert.pem
        //  private/iot-edge-device-{deviceId}.key.pem
        public static Task<EdgeCertificates> GenerateEdgeCertificatesAsync(string deviceId, string scriptPath, CancellationToken token) => IsWindows()
            ? Windows.CertHelper.GenerateEdgeCertificatesAsync(deviceId, scriptPath, token)
            : Linux.CertHelper.GenerateEdgeCertificatesAsync(deviceId, scriptPath, token);

        // After calling this function, the following files will be available under {scriptPath}:
        //  certs/iot-device-{leafDeviceId}-full-chain.cert.pem
        //  private/iot-device-{leafDeviceId}.key.pem
        public static Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, string scriptPath, CancellationToken token) => IsWindows()
            ? Windows.CertHelper.GenerateLeafCertificatesAsync(leafDeviceId, scriptPath, token)
            : Linux.CertHelper.GenerateLeafCertificatesAsync(leafDeviceId, scriptPath, token);

        // After calling this function, the following files will be available under {scriptPath}:
        //  certs/azure-iot-test-only.root.ca.cert.pem
        //  private/azure-iot-test-only.root.ca.key.pem
        public static Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token) => IsWindows()
            ? Windows.CertHelper.InstallRootCertificateAsync(certPath, keyPath, password, scriptPath, token)
            : Linux.CertHelper.InstallRootCertificateAsync(certPath, keyPath, password, scriptPath, token);

        public static void InstallTrustedCertificates(IEnumerable<X509Certificate2> certs, ITransportSettings transportSettings)
        {
            if (IsWindows())
            {
                Windows.CertHelper.InstallTrustedCertificates(certs, transportSettings);
            }
            else
            {
                Linux.CertHelper.InstallTrustedCertificates(certs);
            }
        }

        public static string GetConfigYamlPath() => IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\iotedge\config.yaml"
            : "/etc/iotedge/config.yaml";

        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }
}
