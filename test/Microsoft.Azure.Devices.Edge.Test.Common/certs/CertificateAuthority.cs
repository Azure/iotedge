// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System.Threading;
    using System.Threading.Tasks;
    using RootCaKeys = System.ValueTuple<string, string, string>;

    public class CertificateAuthority
    {
        readonly string scriptPath;

        public EdgeCertificates Certificates { get; }

        public static async Task<CertificateAuthority> CreateAsync(string deviceId, RootCaKeys rootCa, string scriptPath, CancellationToken token)
        {
            (string rootCertificate, string rootPrivateKey, string rootPassword) = rootCa;
            await OsPlatform.Current.InstallRootCertificateAsync(rootCertificate, rootPrivateKey, rootPassword, scriptPath, token);
            EdgeCertificates certs = await OsPlatform.Current.GenerateEdgeCertificatesAsync(deviceId, scriptPath, token);
            return new CertificateAuthority(certs, scriptPath);
        }

        CertificateAuthority(EdgeCertificates certs, string scriptPath)
        {
            this.Certificates = certs;
            this.scriptPath = scriptPath;
        }

        public Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, CancellationToken token) =>
            OsPlatform.Current.GenerateLeafCertificatesAsync(leafDeviceId, this.scriptPath, token);
    }
}
