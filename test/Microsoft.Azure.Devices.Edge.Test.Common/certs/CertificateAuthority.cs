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

        public EdgeCertificates EdgeCertificate { get; }

        public Certificates Certificate { get; }

        public static async Task<CertificateAuthority> CreateAsync(string deviceId, RootCaKeys rootCa, string scriptPath, CancellationToken token)
        {
            (string rootCertificate, string rootPrivateKey, string rootPassword) = rootCa;
            await OsPlatform.Current.InstallRootCertificateAsync(rootCertificate, rootPrivateKey, rootPassword, scriptPath, token);
            EdgeCertificates certs = await OsPlatform.Current.GenerateEdgeCertificatesAsync(deviceId, scriptPath, token);
            return new CertificateAuthority(certs, scriptPath);
        }

        public static async Task<CertificateAuthority> CreateDpsX509CaAsync(string deviceId, RootCaKeys rootCa, string scriptPath, CancellationToken token)
        {
            if (!File.Exists(Path.Combine(scriptPath, "certs", "azure-iot-test-only.root.ca.cert.pem")))
            {
                // [9/26/2019] Setup all the environment for the certificates.
                // More info: /iotedge/scripts/linux/runE2ETest.sh : run_test()
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

            Certificates x509Certificate = await OsPlatform.Current.CreateDeviceCertificatesAsync(deviceId, scriptPath, token);
            return new CertificateAuthority(x509Certificate, scriptPath);
        }

        public static CertificateAuthority GetQuickstart()
        {
            EdgeCertificates certs = OsPlatform.Current.GetEdgeQuickstartCertificates();
            return new CertificateAuthority(certs);
        }

        CertificateAuthority(EdgeCertificates certs)
        {
            this.EdgeCertificate = certs;
            this.scriptPath = Option.None<string>();
        }

        CertificateAuthority(EdgeCertificates certs, string scriptPath)
        {
            this.EdgeCertificate = certs;
            this.scriptPath = Option.Maybe(scriptPath);
        }

        CertificateAuthority(Certificates certs, string scriptPath)
        {
            this.Certificate = certs;
            this.scriptPath = Option.Maybe(scriptPath);
        }

        public Task<Certificates> GenerateLeafCertificatesAsync(string leafDeviceId, CancellationToken token)
        {
            const string Err = "Cannot generate certificates without script";
            string scriptPath = this.scriptPath.Expect(() => new InvalidOperationException(Err));
            return OsPlatform.Current.CreateDeviceCertificatesAsync(leafDeviceId, scriptPath, token);
        }
    }
}
