// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using RootCaKeys = System.ValueTuple<string, string, string>;

    public class CertificateAuthority
    {
        readonly Option<string> scriptPath;

        public EdgeCertificates Certificates { get; }

        public Certificates genericCert { get; }

        public static async Task<CertificateAuthority> CreateAsync(string deviceId, RootCaKeys rootCa, string scriptPath, CancellationToken token)
        {
            (string rootCertificate, string rootPrivateKey, string rootPassword) = rootCa;
            await OsPlatform.Current.InstallRootCertificateAsync(rootCertificate, rootPrivateKey, rootPassword, scriptPath, token);
            EdgeCertificates certs = await OsPlatform.Current.GenerateEdgeCertificatesAsync(deviceId, scriptPath, token);
            return new CertificateAuthority(certs, scriptPath);
        }

        public static async Task<CertificateAuthority> CreateDpsX509CaAsync(string deviceId, RootCaKeys rootCa, string scriptPath, CancellationToken token)
        {
            // [9/26/2019] Setup all the environment for the certificates.
            // More info: /iotedge/scripts/linux/runE2ETest.sh : run_test()
            (string rootCertificate, string rootPrivateKey, string rootPassword) = rootCa;
            await OsPlatform.Current.InstallRootCertificateAsync(rootCertificate, rootPrivateKey, rootPassword, scriptPath, token);

            // BEARWASHERE -- CreateDeviceCertificatesAsync(deviceId, scriptPath, token);
            //  Runs > FORCE_NO_PROD_WARNING="true" ${CERT_SCRIPT_DIR}/certGen.sh create_device_certificate "${registration_id}"
            await OsPlatform.Current.CreateDeviceCertificatesAsync(deviceId, scriptPath, token);

            return new CertificateAuthority(
                new Certificates(
                    Path.Combine(scriptPath, "certs", $"iot-device-{deviceId}-full-chain.cert.pem"),
                    Path.Combine(scriptPath, "private", $"iot-device-{deviceId}.key.pem")),
                scriptPath);
        }

        public static CertificateAuthority GetQuickstart()
        {
            EdgeCertificates certs = OsPlatform.Current.GetEdgeQuickstartCertificates();
            return new CertificateAuthority(certs);
        }

        CertificateAuthority(EdgeCertificates certs)
        {
            this.Certificates = certs;
            this.scriptPath = Option.None<string>();
        }

        CertificateAuthority(EdgeCertificates certs, string scriptPath)
        {
            this.Certificates = certs;
            this.scriptPath = Option.Maybe(scriptPath);
        }

        CertificateAuthority(Certificates certs, string scriptPath)
        {
            this.genericCert = certs;
            this.scriptPath = Option.Maybe(scriptPath);
        }

        public Task<LeafCertificates> GenerateLeafCertificatesAsync(string leafDeviceId, CancellationToken token)
        {
            const string Err = "Cannot generate certificates without script";
            string scriptPath = this.scriptPath.Expect(() => new InvalidOperationException(Err));
            return OsPlatform.Current.GenerateLeafCertificatesAsync(leafDeviceId, scriptPath, token);
        }
    }
}
