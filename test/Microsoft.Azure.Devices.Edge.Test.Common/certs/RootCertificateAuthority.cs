// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System.Threading;
    using System.Threading.Tasks;

    public class RootCertificateAuthority
    {
        readonly string scriptPath;

        public static async Task<RootCertificateAuthority> CreateAsync(string certificatePath, string keyPath, string password, string scriptPath, CancellationToken token)
        {
            await Platform.InstallRootCertificateAsync(certificatePath, keyPath, password, scriptPath, token);
            return new RootCertificateAuthority(
                certificatePath, keyPath, scriptPath);
        }

        RootCertificateAuthority(string certificatePath, string keyPath, string scriptPath)
        {
            this.scriptPath = scriptPath;
        }

        public Task<EdgeCertificateAuthority> CreateEdgeCertificateAuthorityAsync(string deviceId, CancellationToken token) =>
            EdgeCertificateAuthority.CreateAsync(deviceId, this.scriptPath, token);
    }
}
