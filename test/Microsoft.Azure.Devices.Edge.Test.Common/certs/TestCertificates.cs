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
        public static async Task<IdCertificates> GenerateIdentityCertificatesAsync(
            string deviceId,
            string destPath,
            CancellationToken token)
        {
            string scriptPath = Context.Current.CaCertScriptPath.Expect(
                () => new System.InvalidOperationException("Missing CA cert script path (check caCertScriptPath in context.json)"));
            (string, string, string) rootCa = Context.Current.RootCaKeys.Expect(
                () => new System.InvalidOperationException("Missing root CA"));

            CertificateAuthority ca = await CertificateAuthority.CreateAsync(deviceId, rootCa, scriptPath, token);
            return await ca.GenerateIdentityCertificatesAsync(deviceId, destPath, token);
        }

        public static async Task<(CaCertificates, CertificateAuthority ca)> GenerateEdgeCaCertsAsync(
            string deviceId,
            string destPath,
            CancellationToken token)
        {
            string scriptPath = Context.Current.CaCertScriptPath.Expect(
                () => new System.InvalidOperationException("Missing CA cert script path (check caCertScriptPath in context.json)"));
            (string, string, string) rootCa = Context.Current.RootCaKeys.Expect(
                () => new System.InvalidOperationException("Missing root CA"));

            CertificateAuthority ca = await CertificateAuthority.CreateAsync(deviceId, rootCa, scriptPath, token);
            CaCertificates certs = await ca.GenerateCaCertificatesAsync(destPath, token);
            ca.EdgeCertificates = certs;

            return (certs, ca);
        }
    }
}
