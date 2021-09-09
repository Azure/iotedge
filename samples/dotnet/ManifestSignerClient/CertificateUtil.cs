// Copyright (c) Microsoft. All rights reserved.

namespace ManifestSignerClient
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using Newtonsoft.Json.Linq;

    using Org.Webpki.JsonCanonicalizer;

    public class CertificateUtil
    {
        public static string GetBase64CertContent(string certPath)
        {
            var sectionStart = "-----BEGIN CERTIFICATE-----";
            var sectionEnd = "-----END CERTIFICATE-----";

            var content = File.ReadAllText(certPath);

            var startPos = content.IndexOf(sectionStart);

            if (startPos < 0)
            {
                throw new Exception("Bad signer certificate file, maybe not PEM? (missing 'BEGIN CERTIFICATE')");
            }

            startPos += sectionStart.Length;

            var endPos = content.IndexOf(sectionEnd);

            if (endPos < 0)
            {
                throw new Exception("Bad signer certificate file, maybe not PEM? (missing 'END CERTIFICATE')");
            }

            if (startPos >= endPos)
            {
                throw new Exception("Bad signer certificate file, maybe not PEM?");
            }

            try
            {
                var base64part = content[startPos..endPos];
                base64part = base64part.Replace("\n", string.Empty);

                return base64part;
            }
            catch (Exception e)
            {
                throw new Exception("Could not decode PEM signer certificate - invalid base64 section", e);
            }
        }

        private static byte[] GetPrivateKeyFromPem(string keyPath, string algoStr)
        {
            var pemKeyFileContent = default(string);
            using (var file = File.OpenText(keyPath))
            {
                pemKeyFileContent = file.ReadToEnd();
            }

            var sectionStart = "-----BEGIN " + algoStr + " PRIVATE KEY-----";
            var sectionEnd = "-----END " + algoStr + " PRIVATE KEY-----";

            var startPos = pemKeyFileContent.IndexOf(sectionStart);

            if (startPos < 0)
            {
                throw new Exception($"Bad key file, maybe not PEM? (missing 'BEGIN {algoStr} PRIVATE KEY')");
            }

            startPos += sectionStart.Length;

            var endPos = pemKeyFileContent.IndexOf(sectionEnd);

            if (endPos < 0)
            {
                throw new Exception($"Bad key file, maybe not PEM? (missing 'END {algoStr} PRIVATE KEY')");
            }

            if (startPos >= endPos)
            {
                throw new Exception("Bad key file, maybe not PEM?");
            }

            try
            {
                var base64part = pemKeyFileContent[startPos..endPos];
                var result = Convert.FromBase64String(base64part);
                return result;
            }
            catch (Exception e)
            {
                throw new Exception("Could not decode PEM key - invalid base64 section", e);
            }
        }

        private static ECDsa CreateECDsaFromPrivateKey(byte[] signerPrivateKeyContent)
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(signerPrivateKeyContent, out _);

            return ecdsa;
        }

        private static RSA CreateRSAFromPrivateKey(byte[] signerPrivateKeyContent)
        {
            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(signerPrivateKeyContent, out _);
            return rsa;
        }

        public static string GetJsonSignature(string dsaAlgorithmScheme, HashAlgorithmName shaAlgorithm, string content, JObject protectedHeader, string signerKeyPath)
        {
            var canonicalizerContent = new JsonCanonicalizer(content);
            var canonicalizedContent = canonicalizerContent.GetEncodedUTF8();

            var canonicalizerHeader = new JsonCanonicalizer(protectedHeader.ToString());
            var canonicalizedHeader = canonicalizerHeader.GetEncodedUTF8();

            var finalContent = new byte[canonicalizedHeader.Length + canonicalizedContent.Length];

            Array.Copy(canonicalizedHeader, 0, finalContent, 0, canonicalizedHeader.Length);
            Array.Copy(canonicalizedContent, 0, finalContent, canonicalizedHeader.Length, canonicalizedContent.Length);

            if (dsaAlgorithmScheme == "ES" || dsaAlgorithmScheme == "es")
            {
                var signerPrivateKeyContent = GetPrivateKeyFromPem(signerKeyPath, "EC");
                var signer = CreateECDsaFromPrivateKey(signerPrivateKeyContent);
                return Convert.ToBase64String(signer.SignData(finalContent, shaAlgorithm));
            }
            else
            {
                var signerPrivateKeyContent = GetPrivateKeyFromPem(signerKeyPath, "RSA");
                var signer = CreateRSAFromPrivateKey(signerPrivateKeyContent);
                var rsaSignaturePadding = RSASignaturePadding.Pkcs1;
                return Convert.ToBase64String(signer.SignData(finalContent, 0, finalContent.Length, shaAlgorithm, rsaSignaturePadding));
            }
        }
    }
}
