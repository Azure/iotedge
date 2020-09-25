using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Org.Webpki.JsonCanonicalizer;
using Newtonsoft.Json.Linq;

namespace ManifestSignerClient
{
    public class CertificateUtil
    {
        static public string GetBase64CertContent(string certPath)
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
                base64part = base64part.Replace("\n", "");

                return base64part;
            }
            catch (Exception e)
            {
                throw new Exception("Could not decode PEM signer certificate - invalid base64 section", e);
            }
        }
        static public ECDsa CreateECDsaFromPrivateKey(string SignerKeyPath)
        {
            var signerKeyContent = default(string);
            using (var file = File.OpenText(SignerKeyPath))
            {
                signerKeyContent = file.ReadToEnd();
            }

            var derCodedKey = GetDerECDsaPrivateKeyFromPem(signerKeyContent);
            var ecdsa = ECDsa.Create();
            ecdsa.ImportECPrivateKey(derCodedKey, out _);

            return ecdsa;
        }

        static byte[] GetDerECDsaPrivateKeyFromPem(string pemKey)
        {
            var sectionStart = "-----BEGIN EC PRIVATE KEY-----";
            var sectionEnd = "-----END EC PRIVATE KEY-----";

            var startPos = pemKey.IndexOf(sectionStart);

            if (startPos < 0)
            {
                throw new Exception("Bad key file, maybe not PEM? (missing 'BEGIN EC PRIVATE KEY')");
            }

            startPos += sectionStart.Length;

            var endPos = pemKey.IndexOf(sectionEnd);

            if (endPos < 0)
            {
                throw new Exception("Bad key file, maybe not PEM? (missing 'END EC PRIVATE KEY')");
            }

            if (startPos >= endPos)
            {
                throw new Exception("Bad key file, maybe not PEM?");
            }

            try
            {
                var base64part = pemKey.Substring(startPos, endPos - startPos);
                var result = Convert.FromBase64String(base64part);

                return result;
            }
            catch (Exception e)
            {
                throw new Exception("Could not decode PEM key - invalid base64 section", e);
            }
        }

        static public RSA CreateRSAFromPrivateKey(string keyPath)
        {
            var keyFileContent = default(string);
            using (var file = File.OpenText(keyPath))
            {
                keyFileContent = file.ReadToEnd();
            }

            var derCodedKey = GetRSAPrivateKeyFromPem(keyFileContent);

            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(derCodedKey, out _);
            return rsa;
        }

        static byte[] GetRSAPrivateKeyFromPem(string pemKey)
        {
            var sectionStart = "-----BEGIN RSA PRIVATE KEY-----";
            var sectionEnd = "-----END RSA PRIVATE KEY-----";

            var startPos = pemKey.IndexOf(sectionStart);

            if (startPos < 0)
            {
                throw new Exception("Bad key file, maybe not PEM? (missing 'BEGIN RSA PRIVATE KEY')");
            }

            startPos += sectionStart.Length;

            var endPos = pemKey.IndexOf(sectionEnd);

            if (endPos < 0)
            {
                throw new Exception("Bad key file, maybe not PEM? (missing 'END RSA PRIVATE KEY')");
            }

            if (startPos >= endPos)
            {
                throw new Exception("Bad key file, maybe not PEM?");
            }

            try
            {
                var base64part = pemKey[startPos..endPos];
                var result = Convert.FromBase64String(base64part);

                return result;
            }
            catch (Exception e)
            {
                throw new Exception("Could not decode PEM key - invalid base64 section", e);
            }
        }

        static public string GetJsonSignature(string dsaAlgorithmScheme, HashAlgorithmName shaAlgorithm, string content, JObject protectedHeader, string keyPath)
        {
            var canonicalizerContent = new JsonCanonicalizer(content);
            var canonicalizedContent = canonicalizerContent.GetEncodedUTF8();

            var canonicalizerHeader = new JsonCanonicalizer(protectedHeader.ToString());
            var canonicalizedHeader = canonicalizerHeader.GetEncodedUTF8();

            var finalContent = new byte[canonicalizedHeader.Length + canonicalizedContent.Length];

            Array.Copy(canonicalizedHeader, 0, finalContent, 0, canonicalizedHeader.Length);
            Array.Copy(canonicalizedContent, 0, finalContent, canonicalizedHeader.Length, canonicalizedContent.Length);

            if(dsaAlgorithmScheme == "EC")
            {
                var signer = CreateECDsaFromPrivateKey(keyPath);
                return Convert.ToBase64String(signer.SignData(finalContent, shaAlgorithm));
            }
            else if(dsaAlgorithmScheme == "RS")
            {
                var signer = CreateRSAFromPrivateKey(keyPath);
                var rsaSignaturePadding = RSASignaturePadding.Pkcs1;
                return Convert.ToBase64String(signer.SignData(finalContent, 0, finalContent.Length, shaAlgorithm, rsaSignaturePadding));
            }
            else
            {
                throw new Exception("DSA algorithm not supported");
            }
        }
    }
}
