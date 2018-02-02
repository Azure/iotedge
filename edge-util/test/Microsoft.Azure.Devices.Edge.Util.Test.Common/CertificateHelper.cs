// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using Org.BouncyCastle.Asn1.X509;
    using Org.BouncyCastle.Crypto;
    using Org.BouncyCastle.Crypto.Generators;
    using Org.BouncyCastle.Crypto.Operators;
    using Org.BouncyCastle.Crypto.Prng;
    using Org.BouncyCastle.Math;
    using Org.BouncyCastle.Security;
    using Org.BouncyCastle.X509;
    using BCX509 = Org.BouncyCastle.X509;

    public static class CertificateHelper
    {
        public static X509Certificate2 GetCertificate(string thumbprint, 
            StoreName storeName, 
            StoreLocation storeLocation)
        {
            Preconditions.CheckNonWhiteSpace(thumbprint, nameof(thumbprint));
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection col = store.Certificates.Find(X509FindType.FindByThumbprint, 
                thumbprint, 
                false);
            if (col != null && col.Count > 0)
            {
                return col[0];
            }
            return null;
        }

        public static X509Certificate2 GenerateSelfSignedCert(string subjectName)
        {
            var keyGenerator = new RsaKeyPairGenerator();
            var random = new SecureRandom(new CryptoApiRandomGenerator());
            keyGenerator.Init(new KeyGenerationParameters(random, 1024));

            AsymmetricCipherKeyPair keyPair = keyGenerator.GenerateKeyPair();

            var certName = new X509Name($"CN={subjectName}");
            BigInteger serialNo = BigInteger.ProbablePrime(120, random);
            var certGenerator = new X509V3CertificateGenerator();
            certGenerator.SetSerialNumber(serialNo);
            certGenerator.SetSubjectDN(certName);
            certGenerator.SetIssuerDN(certName);
            certGenerator.SetNotAfter(DateTime.Now.AddYears(10));
            certGenerator.SetNotBefore(DateTime.Now.Subtract(TimeSpan.FromDays(2)));
            certGenerator.SetPublicKey(keyPair.Public);

            var signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", keyPair.Private, random);
            BCX509.X509Certificate bcCert = certGenerator.Generate(signatureFactory);

            return new X509Certificate2(DotNetUtilities.ToX509Certificate(bcCert));
        }
    }
}
