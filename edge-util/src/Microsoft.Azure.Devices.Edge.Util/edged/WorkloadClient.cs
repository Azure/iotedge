// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.Threading.Tasks;

    public class WorkloadClient
    {
        readonly WorkloadClientVersioned inner;

        public WorkloadClient(Uri serverUri, string edgeletApiVersion, string edgeletClientApiVersion, string moduleId, string moduleGenerationId)
        {
            Preconditions.CheckNotNull(serverUri, nameof(serverUri));
            Preconditions.CheckNonWhiteSpace(edgeletApiVersion, nameof(edgeletApiVersion));
            Preconditions.CheckNonWhiteSpace(edgeletClientApiVersion, nameof(edgeletClientApiVersion));
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            Preconditions.CheckNonWhiteSpace(moduleGenerationId, nameof(moduleGenerationId));

            this.inner = this.GetVersionedWorkloadClient(serverUri, edgeletApiVersion, edgeletClientApiVersion, moduleId, moduleGenerationId);
        }

        public Task<ServerCertificateResponse> CreateServerCertificateAsync(string hostname, DateTime expiration) => this.inner.CreateServerCertificateAsync(hostname, expiration);

        public Task<string> GetTrustBundleAsync() => this.inner.GetTrustBundleAsync();

        public Task<string> EncryptAsync(string initializationVector, string plainText) => this.inner.EncryptAsync(initializationVector, plainText);

        public Task<string> DecryptAsync(string initializationVector, string encryptedText) => this.inner.DecryptAsync(initializationVector, encryptedText);

        public Task<string> SignAsync(string keyId, string algorithm, string data) => this.inner.SignAsync(keyId, algorithm, data);

        internal WorkloadClientVersioned GetVersionedWorkloadClient(Uri workloadUri, string edgeletApiVersion, string edgeletClientApiVersion, string moduleId, string moduleGenerationId)
        {
            ApiVersion supportedVersion = this.GetSupportedVersion(edgeletApiVersion, edgeletClientApiVersion);
            if (supportedVersion == ApiVersion.Version20180628)
            {
                return new Version_2018_06_28.WorkloadClient(workloadUri, supportedVersion, moduleId, moduleGenerationId);
            }

            if (supportedVersion == ApiVersion.Version20181230)
            {
                return new Version_2018_12_30.WorkloadClient(workloadUri, supportedVersion, moduleId, moduleGenerationId);
            }

            return new Version_2018_06_28.WorkloadClient(workloadUri, supportedVersion, moduleId, moduleGenerationId);
        }

        ApiVersion GetSupportedVersion(string edgeletApiVersion, string edgeletManagementApiVersion)
        {
            var serverVersion = (ApiVersion)edgeletApiVersion;
            var clientVersion = (ApiVersion)edgeletManagementApiVersion;

            return serverVersion.Value < clientVersion.Value ? serverVersion : clientVersion;
        }
    }
}
