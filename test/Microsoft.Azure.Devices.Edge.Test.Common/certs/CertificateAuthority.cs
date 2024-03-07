// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using RootCaKeys = System.ValueTuple<string, string, string>;

    public class CertificateAuthority
    {
        readonly string scriptPath;
        readonly string deviceId;

        // TODO: EdgeCertificates needs to be moved into certificate types.
        public CaCertificates EdgeCertificates { get; set; }

        public static async Task<CertificateAuthority> CreateAsync(string deviceId, RootCaKeys rootCa, string scriptPath, CancellationToken token)
        {
            if (!File.Exists(Path.Combine(scriptPath, FixedPaths.RootCaCert.Cert)))
            {
                // Setup all the environment for the certificates.
                Log.Verbose("----------------------------------------");
                Log.Verbose("Install Root Certificate");
                (string rootCertificate, string rootPrivateKey, string rootPassword) = rootCa;
                await OsPlatform.Current.InstallRootCertificateAsync(rootCertificate, rootPrivateKey, rootPassword, scriptPath, token);
            }
            else
            {
                Log.Verbose("----------------------------------------");
                Log.Verbose("Skip Root Certificate: Root Certificate is installed");
                Log.Verbose("----------------------------------------");
            }

            return new CertificateAuthority(deviceId, scriptPath);
        }

        CertificateAuthority(string deviceId, string scriptPath)
        {
            this.deviceId = deviceId;
            this.scriptPath = scriptPath;
        }

        public Task<IdCertificates> GenerateIdentityCertificatesAsync(string uniqueId, string destPath, CancellationToken token) =>
            OsPlatform.Current.GenerateIdentityCertificatesAsync(uniqueId, this.scriptPath, destPath, token);

        public Task<CaCertificates> GenerateCaCertificatesAsync(string destPath, CancellationToken token) =>
            OsPlatform.Current.GenerateCaCertificatesAsync(this.deviceId, this.scriptPath, destPath, token);
    }
}
