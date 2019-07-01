// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System.Threading;
    using System.Threading.Tasks;

    public class EdgeCertificateAuthority
    {
        readonly string scriptPath;

        public EdgeCertificates Certificates { get; }

        public static async Task<EdgeCertificateAuthority> CreateAsync(string deviceId, string scriptPath, CancellationToken token)
        {
            EdgeCertificates certs = await Platform.GenerateEdgeCertificatesAsync(deviceId, scriptPath, token);
            return new EdgeCertificateAuthority(certs, scriptPath);
        }

        EdgeCertificateAuthority(EdgeCertificates certs, string scriptPath)
        {
            this.Certificates = certs;
            this.scriptPath = scriptPath;
        }

        public Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, CancellationToken token) =>
            Platform.GenerateLeafCertificatesAsync(leafDeviceId, this.scriptPath, token);
    }
}
