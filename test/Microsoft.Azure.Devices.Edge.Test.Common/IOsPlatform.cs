// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IOsPlatform
    {
        Task<string> CollectDaemonLogsAsync(DateTime testStartTime, string filePrefix, CancellationToken token);

        IEdgeDaemon CreateEdgeDaemon(Option<string> installerPath);

        // After calling this function, the following files will be available under {scriptPath}:
        //  certs/iot-edge-device-{deviceId}-full-chain.cert.pem
        //  private/iot-edge-device-{deviceId}.key.pem
        Task<EdgeCertificates> GenerateEdgeCertificatesAsync(string deviceId, string scriptPath, CancellationToken token);

        // After calling this function, the following files will be available under {scriptPath}:
        //  certs/iot-device-{leafDeviceId}-full-chain.cert.pem
        //  private/iot-device-{leafDeviceId}.key.pem
        Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, string scriptPath, CancellationToken token);

        void InstallEdgeCertificates(IEnumerable<X509Certificate2> certs, ITransportSettings transportSettings);

        // After calling this function, the following files will be available under {scriptPath}:
        //  certs/azure-iot-test-only.root.ca.cert.pem
        //  private/azure-iot-test-only.root.ca.key.pem
        Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token);

        void InstallTrustedCertificates(IEnumerable<X509Certificate2> certs);
    }
}
