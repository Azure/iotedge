// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Edged.Version_2019_01_30
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Edged.Version_2019_01_30.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;

    class WorkloadClient : WorkloadClientVersioned
    {
        public WorkloadClient(Uri serverUri, ApiVersion apiVersion, string moduleId, string moduleGenerationId)
            : base(serverUri, apiVersion, moduleId, moduleGenerationId, new ErrorDetectionStrategy())
        {
        }

        public override async Task<ServerCertificateResponse> CreateServerCertificateAsync(string hostname, DateTime expiration)
        {
            var request = new ServerCertificateRequest
            {
                CommonName = hostname,
                Expiration = expiration
            };

            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.WorkloadUri))
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.WorkloadUri) };
                CertificateResponse result = await this.Execute(() => edgeletHttpClient.CreateServerCertificateAsync(this.Version.Name, this.ModuleId, this.ModuleGenerationId, request), "CreateServerCertificateAsync");
                return new ServerCertificateResponse()
                {
                    Certificate = result.Certificate,
                    PrivateKey = result.PrivateKey.Bytes
                };
            }
        }

        public override async Task<string> GetTrustBundleAsync()
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.WorkloadUri))
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.WorkloadUri) };
                TrustBundleResponse result = await this.Execute(() => edgeletHttpClient.TrustBundleAsync(this.Version.Name), "TrustBundleAsync");
                return result.Certificate;
            }
        }

        public override async Task<string> EncryptAsync(string initializationVector, string plainText)
        {
            var request = new EncryptRequest
            {
                Plaintext = Encoding.UTF8.GetBytes(plainText),
                InitializationVector = Encoding.UTF8.GetBytes(initializationVector)
            };
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.WorkloadUri))
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.WorkloadUri) };
                EncryptResponse result = await this.Execute(() => edgeletHttpClient.EncryptAsync(this.Version.Name, this.ModuleId, this.ModuleGenerationId, request), "Encrypt");
                return Convert.ToBase64String(result.Ciphertext);
            }
        }

        public override async Task<string> DecryptAsync(string initializationVector, string encryptedText)
        {
            var request = new DecryptRequest
            {
                Ciphertext = Convert.FromBase64String(encryptedText),
                InitializationVector = Encoding.UTF8.GetBytes(initializationVector)
            };
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.WorkloadUri))
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.WorkloadUri) };
                DecryptResponse result = await this.Execute(() => edgeletHttpClient.DecryptAsync(this.Version.Name, this.ModuleId, this.ModuleGenerationId, request), "Decrypt");
                return Encoding.UTF8.GetString(result.Plaintext);
            }
        }

        public override async Task<string> SignAsync(string keyId, string algorithm, string data)
        {
            var signRequest = new SignRequest
            {
                KeyId = keyId,
                Algo = this.GetSignatureAlgorithm(algorithm),
                Data = Encoding.UTF8.GetBytes(data)
            };

            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.WorkloadUri))
            {
                var edgeletHttpClient = new HttpWorkloadClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.WorkloadUri) };
                SignResponse response = await this.Execute(() => edgeletHttpClient.SignAsync(this.Version.Name, this.ModuleId, this.ModuleGenerationId, signRequest), "SignAsync");
                return Convert.ToBase64String(response.Digest);
            }
        }

        protected override void HandleException(Exception ex, string operation)
        {
            switch (ex)
            {
                case IoTEdgedException<ErrorResponse> errorResponseException:
                    throw new WorkloadCommunicationException($"Error calling {operation}: {errorResponseException.Result?.Message ?? string.Empty}", errorResponseException.StatusCode);

                case IoTEdgedException swaggerException:
                    if (swaggerException.StatusCode < 400)
                    {
                        return;
                    }
                    else
                    {
                        throw new WorkloadCommunicationException($"Error calling {operation}: {swaggerException.Response ?? string.Empty}", swaggerException.StatusCode);
                    }

                default:
                    throw ex;
            }
        }

        SignRequestAlgo GetSignatureAlgorithm(string algorithm)
        {
            // for now there is only one supported algorithm
            return SignRequestAlgo.HMACSHA256;
        }

        class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex) => ex is IoTEdgedException se
                                                     && se.StatusCode >= 500;
        }
    }
}
