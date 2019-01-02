// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.IdentityModel.Tokens;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    class ClientAssertionCertificate : IClientAssertionCertificate
    {
        readonly X509Certificate2 certificate;

        public ClientAssertionCertificate(string clientId, X509Certificate2 clientAssertionCertPfx)
        {
            this.ClientId = clientId;
            this.certificate = clientAssertionCertPfx;
        }
        
        public string ClientId { get; }

        public string Thumbprint => Base64UrlEncoder.Encode(this.certificate.GetCertHash());

        public byte[] Sign(string message)
        {
            using (RSA key = this.certificate.GetRSAPrivateKey())
            {
                return key.SignData(Encoding.UTF8.GetBytes(message), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }
    }
}