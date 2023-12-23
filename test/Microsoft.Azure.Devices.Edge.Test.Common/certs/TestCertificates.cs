// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;

    // Generates a test CA cert, test CA key, and trust bundle.
    // TODO: Remove this once iotedge init is finished?
    public class TestCertificates
    {
        private readonly string deviceId;
        private readonly CaCertificates certs;

        public CaCertificates CaCertificates => this.certs;

        TestCertificates(string deviceId, CaCertificates certs)
        {
            this.deviceId = deviceId;
            this.certs = certs;
        }

        public static async Task<(TestCertificates, CertificateAuthority ca)> GenerateCertsAsync(string deviceId, CancellationToken token)
        {
            string scriptPath = Context.Current.CaCertScriptPath.Expect(
                () => new System.InvalidOperationException("Missing CA cert script path (check caCertScriptPath in context.json)"));
            (string, string, string) rootCa = Context.Current.RootCaKeys.Expect(
                () => new System.InvalidOperationException("Missing root CA"));
            string destPath = Path.Combine(FixedPaths.E2E_TEST_DIR, deviceId);

            CertificateAuthority ca = await CertificateAuthority.CreateAsync(deviceId, rootCa, scriptPath, token);
            CaCertificates certs = await ca.GenerateCaCertificatesAsync(destPath, token);
            ca.EdgeCertificates = certs;

            return (new TestCertificates(deviceId, certs), ca);
        }
    }
}
