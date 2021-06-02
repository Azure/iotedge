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

        Task<IEdgeDaemon> CreateEdgeDaemonAsync(Option<string> installerPath, CancellationToken token);

        // After calling this function, the following files will be available under {scriptPath}:
        //  certs/iot-device-{deviceId}-full-chain.cert.pem
        //  private/iot-device-{deviceId}.key.pem
        Task<IdCertificates> GenerateIdentityCertificatesAsync(string deviceId, string scriptPath, CancellationToken token);

        // After calling this function, the following files will be available under {scriptPath}:
        //  certs/iot-edge-device-{deviceId}-full-chain.cert.pem
        //  private/iot-edge-device-{deviceId}.key.pem
        Task<CaCertificates> GenerateCaCertificatesAsync(string deviceId, string scriptPath, CancellationToken token);

        CaCertificates GetEdgeQuickstartCertificates(string deviceId);

        void InstallCaCertificates(IEnumerable<X509Certificate2> certs, ITransportSettings transportSettings);

        // After calling this function, the following files will be available under {scriptPath}:
        //  certs/azure-iot-test-only.root.ca.cert.pem
        //  private/azure-iot-test-only.root.ca.key.pem
        Task InstallRootCertificateAsync(string certPath, string keyPath, string password, string scriptPath, CancellationToken token);

        void InstallTrustedCertificates(IEnumerable<X509Certificate2> certs);

        void SetOwner(string filePath, string owner, string permissions);

        uint GetUid(string user);
    }
}
