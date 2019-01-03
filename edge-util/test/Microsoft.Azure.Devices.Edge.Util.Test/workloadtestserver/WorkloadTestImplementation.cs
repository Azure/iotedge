// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common.WorkloadTestServer
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.WorkloadTestServer.Controllers;

    class WorkloadTestImplementation : IController
    {
        public Task<SignResponse> SignAsync(string api_version, string name, string genid, SignRequest payload)
        {
            using (var algorithm = new HMACSHA256(Encoding.UTF8.GetBytes("key")))
            {
                var response = new SignResponse()
                {
                    Digest = algorithm.ComputeHash(payload.Data)
                };

                return Task.FromResult(response);
            }
        }

        public Task<EncryptResponse> EncryptAsync(string api_version, string name, string genid, EncryptRequest payload)
        {
            string encrypted = Encoding.UTF8.GetString(payload.InitializationVector) + Encoding.UTF8.GetString(payload.Plaintext);
            return Task.FromResult(new EncryptResponse() { Ciphertext = Encoding.UTF8.GetBytes(encrypted) });
        }

        public Task<DecryptResponse> DecryptAsync(string api_version, string name, string genid, DecryptRequest payload)
        {
            string dencrypted = Encoding.UTF8.GetString(payload.InitializationVector) + Encoding.UTF8.GetString(payload.Ciphertext);
            return Task.FromResult(new DecryptResponse() { Plaintext = Encoding.UTF8.GetBytes(dencrypted) });
        }

        public Task<CertificateResponse> CreateIdentityCertificateAsync(string api_version, string name, string genid) => throw new System.NotImplementedException();

        public Task<CertificateResponse> CreateServerCertificateAsync(string api_version, string name, string genid, ServerCertificateRequest request)
        {
            var response = new CertificateResponse()
            {
                Certificate =  $"{CertificateHelper.CertificatePem}\n{CertificateHelper.CertificatePem}",
                PrivateKey = new PrivateKey()
                {
                    Type = PrivateKeyType.Key,
                    Bytes = CertificateHelper.PrivateKeyPem
                }
            };

            return Task.FromResult(response);
        }

        public Task<TrustBundleResponse> TrustBundleAsync(string api_version)
        {
            var response = new TrustBundleResponse();
            response.Certificate = CertificateHelper.CertificatePem;

            return Task.FromResult(response);
        }
    }
}
