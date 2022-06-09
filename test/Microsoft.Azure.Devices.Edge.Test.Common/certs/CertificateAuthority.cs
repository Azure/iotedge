// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;
    using RootCaKeys = System.ValueTuple<string, string, string>;

    public class CertificateAuthority
    {
        readonly Option<string> scriptPath;

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

            return new CertificateAuthority(scriptPath);
        }

        public static CertificateAuthority GetQuickstart(string deviceId)
        {
            CaCertificates certs = OsPlatform.Current.GetEdgeQuickstartCertificates(deviceId);
            return new CertificateAuthority(certs);
        }

        CertificateAuthority(string scriptPath)
        {
            this.scriptPath = Option.Maybe(scriptPath);
        }

        CertificateAuthority(CaCertificates certs)
        {
            this.scriptPath = Option.None<string>();
            this.EdgeCertificates = certs;
        }

        public Task<IdCertificates> GenerateIdentityCertificatesAsync(string deviceId, CancellationToken token)
        {
            const string Err = "Cannot generate certificates without script";
            string scriptPath = this.scriptPath.Expect(() => new InvalidOperationException(Err));
            return OsPlatform.Current.GenerateIdentityCertificatesAsync(deviceId, scriptPath, token);
        }

        public Task<CaCertificates> GenerateCaCertificatesAsync(string deviceId, CancellationToken token)
        {
            const string Err = "Cannot generate certificates without script";
            string scriptPath = this.scriptPath.Expect(() => new InvalidOperationException(Err));
            return OsPlatform.Current.GenerateCaCertificatesAsync(deviceId, scriptPath, token);
        }
    }
}
