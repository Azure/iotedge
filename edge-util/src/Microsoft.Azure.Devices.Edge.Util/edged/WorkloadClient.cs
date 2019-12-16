// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.Threading.Tasks;

    public class WorkloadClient
    {
        readonly WorkloadClientVersioned inner;

        public WorkloadClient(Uri serverUri, string serverSupportedApiVersion, string clientSupportedApiVersion, string moduleId, string moduleGenerationId)
        {
            Preconditions.CheckNotNull(serverUri, nameof(serverUri));
            Preconditions.CheckNonWhiteSpace(serverSupportedApiVersion, nameof(serverSupportedApiVersion));
            Preconditions.CheckNonWhiteSpace(clientSupportedApiVersion, nameof(clientSupportedApiVersion));
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            Preconditions.CheckNonWhiteSpace(moduleGenerationId, nameof(moduleGenerationId));

            this.inner = this.GetVersionedWorkloadClient(serverUri, serverSupportedApiVersion, clientSupportedApiVersion, moduleId, moduleGenerationId);
        }

        public Task<ServerCertificateResponse> CreateServerCertificateAsync(string hostname, DateTime expiration) => this.inner.CreateServerCertificateAsync(hostname, expiration);

        public Task<string> GetTrustBundleAsync() => this.inner.GetTrustBundleAsync();

        public Task<string> EncryptAsync(string initializationVector, string plainText) => this.inner.EncryptAsync(initializationVector, plainText);

        public Task<string> DecryptAsync(string initializationVector, string encryptedText) => this.inner.DecryptAsync(initializationVector, encryptedText);

        public Task<string> SignAsync(string keyId, string algorithm, string data) => this.inner.SignAsync(keyId, algorithm, data);

        internal WorkloadClientVersioned GetVersionedWorkloadClient(Uri workloadUri, string serverSupportedApiVersion, string clientSupportedApiVersion, string moduleId, string moduleGenerationId)
        {
            ApiVersion supportedVersion = this.GetSupportedVersion(serverSupportedApiVersion, clientSupportedApiVersion);
            if (supportedVersion == ApiVersion.Version20180628)
            {
                return new Version_2018_06_28.WorkloadClient(workloadUri, supportedVersion, moduleId, moduleGenerationId);
            }

            if (supportedVersion == ApiVersion.Version20190130)
            {
                return new Version_2019_01_30.WorkloadClient(workloadUri, supportedVersion, moduleId, moduleGenerationId);
            }

            return new Version_2018_06_28.WorkloadClient(workloadUri, supportedVersion, moduleId, moduleGenerationId);
        }

        ApiVersion GetSupportedVersion(string serverSupportedApiVersion, string clientSupportedApiVersion)
        {
            var serverVersion = ApiVersion.ParseVersion(serverSupportedApiVersion);
            var clientVersion = ApiVersion.ParseVersion(clientSupportedApiVersion);

            if (clientVersion == ApiVersion.VersionUnknown)
            {
                throw new InvalidOperationException($"Client version {clientSupportedApiVersion} is not supported.");
            }

            if (serverVersion == ApiVersion.VersionUnknown)
            {
                return clientVersion;
            }

            return serverVersion.Value < clientVersion.Value ? serverVersion : clientVersion;
        }
    }
}
