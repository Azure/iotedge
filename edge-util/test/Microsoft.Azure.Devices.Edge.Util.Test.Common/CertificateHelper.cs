// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using Org.BouncyCastle.Asn1;
    using Org.BouncyCastle.Asn1.X509;
    using Org.BouncyCastle.Crypto;
    using Org.BouncyCastle.Crypto.Generators;
    using Org.BouncyCastle.Crypto.Operators;
    using Org.BouncyCastle.Crypto.Prng;
    using Org.BouncyCastle.Math;
    using Org.BouncyCastle.Security;
    using Org.BouncyCastle.X509.Extension;
    using BCX509 = Org.BouncyCastle.X509;

    public static class CertificateHelper
    {
        public const string CertificatePem = @"-----BEGIN CERTIFICATE-----
MIIEszCCApugAwIBAgIBKTANBgkqhkiG9w0BAQsFADAdMRswGQYDVQQDDBJUZXN0
IEVkZ2UgT3duZXIgQ0EwHhcNMTgwNTIzMDAzMjQzWhcNMTgwODIxMDAzMjQzWjAd
MRswGQYDVQQDDBJUZXN0IEVkZ2UgT3duZXIgQ0EwggIiMA0GCSqGSIb3DQEBAQUA
A4ICDwAwggIKAoICAQDaDQIZm/VDDbUf5/qmO6IQiKPteetPdonYo0hYOyfkK+9n
99jQWO65ue5f2lU5RubjL8ewL8AtnHqQuQEvjUQPu4aMSH+VySStK27dfn8HQ6ut
39OuZIWqs1rWP5VRfY6inXUaZxmfllj1skStkqSJNCcW0Bp4xtt85V6nKTbEKj8F
UI1aCG3lfdMRFfxontJv/vu+91lQ10pucoUfKrpw6C4BHIMzpY8hZKVnVki8Y60b
eLAGqYA85Nlb5xbCAkErDEkjRf7b/y474MJHhlhzELsfhaswqYqoF83+PMPYXBkr
8BM4QAFO9rSep8CBHvrA0zC3fbMwsDCPfk4x3GyCtVg90TZbj2bKN61elLkNKhgE
S60uuegz8zHYqpy/bAj0XwnwBYguQ2RiYVeOTw7ubE+0VzVLp5ISJJsise6AafvM
ghJVfPjONodAs5Bw9mai61RdxGdgLWnE6sXhfBKQ5rl9Z8NlbrGCTQVgdtJK4oti
w66T71iqS69IDh85GU3gWNoYf2L4i6x/KOI3o+5anK+u06YFzpXLvtDcMnxug11Q
9C8DlcCEBAB6DG6kwFPTv2qlBLz10SMBJ3wrVu4sb9I7MYoNh1Jytg/XRM/zZkCu
o3jA+Jr6Dubw9TdYnP8YtP2A9vYSnZkiu4HaY3MhkXDC1cpWFa8rGqNxbS/lqQID
AQABMA0GCSqGSIb3DQEBCwUAA4ICAQAWpeB8H7H7das8+uhjugdvu406kCnCUC6T
NwvzHBj3naUUTJhMYj5fmT3C4DnyEdr257UfQZ3v1hBGxXdg4ZRyfHycvEKhP6LL
E/9J9LrMy6G/QFAH8y4IAtNtLeDURBcCBg+ZqaDdIm1e4QY1qHcfH/pBG6tj0Dnk
Y4GRSM/ClAzlzhoLqFE/wYNM4VKHJpWt2fw2y3hP4saNEJlKqDkD4ezHJ5zW75W+
XYJoWo+giOkkajH+GzVdpScT8OpiOM5wJQ0qn/OLTu0oZZMDClhNeCNRYYWvT7pB
am/REGW2DZ6twZbfQXkUjf52qQ7LK31OqY59zLajE+O2g9aJ9PGiFKh925Gn6Vw2
b+VWn/Eb/qDq/ItrW+FgGZ+dyAxRaZGIugDNOw46juM90yVLJizlkScHf9pLU7uO
M1E9d71JkHrKc8XOkPtJ6kx3VYScJ0rhCJNQhbAxPYd+ahux3wiCbZcO2CyeyZQx
XL0MLwajpaUPCbZ2tALRjoeH73B95CMGYYXSGp4d9a/ttprXqFEuHEGbJKvHPTZ2
x2RXGozmy6wV2uM0vc3fyUu+rse2HmBbDfVXul3EsXl3JmImdySc65CR/gczbULm
P32rJqhICRJq9XTuRbrpeVFSazHwYsfwfaoh3oKh8CXZnyvibZ1lpER+DSn+P8mi
jakkDyV11Q==
-----END CERTIFICATE-----
";

        public const string PrivateKeyPem = @"-----BEGIN PRIVATE KEY-----
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
U7JoTvzy0x7VG98T0+y68IcyjsSIPQ==
-----END PRIVATE KEY-----
";

        public enum ExtKeyUsage
        {
            None = 0,
            ClientAuth,
            ServerAuth,
        }

        public static X509Certificate2 GetCertificate(
            string thumbprint,
            StoreName storeName,
            StoreLocation storeLocation)
        {
            Preconditions.CheckNonWhiteSpace(thumbprint, nameof(thumbprint));
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection col = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                thumbprint,
                false);
            if (col != null && col.Count > 0)
            {
                return col[0];
            }

            return null;
        }

        public static X509Certificate2 GenerateSelfSignedCert(string subjectName, bool isCA = false)
        {
            var (cert, keyPair) = GenerateSelfSignedCert(subjectName, DateTime.Now.Subtract(TimeSpan.FromDays(2)), DateTime.Now.AddYears(10), isCA);
            return cert;
        }

        public static (X509Certificate2, AsymmetricCipherKeyPair) GenerateSelfSignedCert(string subjectName, DateTime notBefore, DateTime notAfter, bool isCA) =>
            GenerateCertificate(subjectName, notBefore, notAfter, null, null, isCA, null, null);

        public static (X509Certificate2, AsymmetricCipherKeyPair) GenerateServerCert(string subjectName, DateTime notBefore, DateTime notAfter) =>
            GenerateCertificate(subjectName, notBefore, notAfter, null, null, false, null, new List<ExtKeyUsage>() { ExtKeyUsage.ServerAuth });

        public static (X509Certificate2, AsymmetricCipherKeyPair) GenerateClientert(string subjectName, DateTime notBefore, DateTime notAfter) =>
            GenerateCertificate(subjectName, notBefore, notAfter, null, null, false, null, new List<ExtKeyUsage>() { ExtKeyUsage.ClientAuth });

        public static (X509Certificate2, AsymmetricCipherKeyPair) GenerateCertificate(
            string subjectName,
            DateTime notBefore,
            DateTime notAfter,
            X509Certificate2 issuer,
            AsymmetricCipherKeyPair issuerKeyPair,
            bool isCA,
            GeneralNames sanEntries,
            IList<ExtKeyUsage> extKeyUsages)
        {
            if (((issuer == null) && (issuerKeyPair != null)) ||
                ((issuer != null) && (issuerKeyPair == null)))
            {
                throw new ArgumentException("Issuer and Issuer key pair must both be null or non null");
            }

            var keyGenerator = new RsaKeyPairGenerator();
            var random = new SecureRandom(new CryptoApiRandomGenerator());
            keyGenerator.Init(new KeyGenerationParameters(random, 1024));

            AsymmetricCipherKeyPair keyPair = keyGenerator.GenerateKeyPair();

            var certName = new X509Name($"CN={subjectName}");
            BigInteger serialNo = BigInteger.ProbablePrime(120, random);
            var certGenerator = new BCX509.X509V3CertificateGenerator();
            certGenerator.SetSerialNumber(serialNo);
            certGenerator.SetSubjectDN(certName);
            certGenerator.SetNotAfter(notAfter);
            certGenerator.SetNotBefore(notBefore);
            certGenerator.SetPublicKey(keyPair.Public);
            certGenerator.AddExtension(X509Extensions.SubjectKeyIdentifier, false, new SubjectKeyIdentifierStructure(keyPair.Public));
            if (isCA)
            {
                certGenerator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(isCA));
            }

            if (sanEntries != null)
            {
                certGenerator.AddExtension(X509Extensions.SubjectAlternativeName, false, sanEntries);
            }

            if (issuer != null)
            {
                certGenerator.SetIssuerDN(new X509Name(issuer.Subject));
                var issuerCert = DotNetUtilities.FromX509Certificate(issuer);
                certGenerator.AddExtension(X509Extensions.AuthorityKeyIdentifier, false, new AuthorityKeyIdentifierStructure(issuerCert));
            }
            else
            {
                certGenerator.SetIssuerDN(certName);
            }

            if (extKeyUsages != null)
            {
                var oids = new List<DerObjectIdentifier>();
                foreach (var usage in extKeyUsages)
                {
                    if (usage == ExtKeyUsage.ClientAuth)
                    {
                        oids.Add(new DerObjectIdentifier("1.3.6.1.5.5.7.3.8"));
                    }
                    else if (usage == ExtKeyUsage.ServerAuth)
                    {
                        oids.Add(new DerObjectIdentifier("1.3.6.1.5.5.7.3.1"));
                    }
                }

                var ext = new ExtendedKeyUsage(oids);
                certGenerator.AddExtension(X509Extensions.ExtendedKeyUsage, false, ext);
            }

            var privateKey = (issuerKeyPair == null) ? keyPair.Private : issuerKeyPair.Private;
            var signatureFactory = new Asn1SignatureFactory("SHA256WITHRSA", privateKey, random);
            BCX509.X509Certificate bcCert = certGenerator.Generate(signatureFactory);

            var cert = new X509Certificate2(DotNetUtilities.ToX509Certificate(bcCert));
            return (cert, keyPair);
        }

        public static GeneralNames PrepareSanEntries(IList<string> uris, IList<string> dnsNames)
        {
            int totalCount = uris.Count + dnsNames.Count;
            if (totalCount == 0)
            {
                throw new ArgumentException($"Total entries count is zero. uris:{uris.Count}, dnsNames:{dnsNames.Count}");
            }

            GeneralName[] names = new GeneralName[totalCount];

            int index = 0;
            foreach (string value in uris)
            {
                names[index++] = new GeneralName(GeneralName.UniformResourceIdentifier, value);
            }

            foreach (string value in dnsNames)
            {
                names[index++] = new GeneralName(GeneralName.DnsName, value);
            }

            GeneralNames subjectAltNames = new GeneralNames(names);

            return subjectAltNames;
        }
    }
}
