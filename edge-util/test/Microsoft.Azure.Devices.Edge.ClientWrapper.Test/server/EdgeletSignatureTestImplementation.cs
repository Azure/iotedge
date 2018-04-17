// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.ClientWrapper.Test.Server
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ClientWrapper.Test.Server.Controllers;

    class EdgeletSignatureTestImplementation : IController
    {
        public Task<CertificateResponse> CreateIdentityCertificateAsync(string apiVersion, string name)
        {
            throw new NotImplementedException();
        }

        public Task<CertificateResponse> CreateServerCertificateAsync(string apiVersion, string name, ServerCertificateRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<SignResponse> SignAsync(string apiVersion, string name, SignRequest payload)
        {
            using (var algorithm = new HMACSHA256(Encoding.UTF8.GetBytes(payload.KeyId)))
            {
                return Task.FromResult(new SignResponse()
                {
                    Digest = algorithm.ComputeHash(payload.Data)
                });
            }
        }
    }
}
