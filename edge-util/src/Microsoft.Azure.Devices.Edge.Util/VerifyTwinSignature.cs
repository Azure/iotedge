// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    using Org.Webpki.JsonCanonicalizer;
    public class VerifyTwinSignature
    {
        public static bool VerifyModuleTwinSignature(string desiredProperties, string header, byte[] signatureBytes, X509Certificate2 signerCert, string algorithmScheme, HashAlgorithmName hashAlgorithm)
        {
            try
            {
                JsonCanonicalizer canonicalizerDesiredProperties = new JsonCanonicalizer(desiredProperties);
                byte[] canonicalizedDesiredProperties = canonicalizerDesiredProperties.GetEncodedUTF8();

                JsonCanonicalizer canonicalizerHeader = new JsonCanonicalizer(header);
                byte[] canonicalizedHeader = canonicalizerHeader.GetEncodedUTF8();

                byte[] canonicalizedCombinedDesiredProperties = new byte[canonicalizedHeader.Length + canonicalizedDesiredProperties.Length];

                Array.Copy(canonicalizedHeader, 0, canonicalizedCombinedDesiredProperties, 0, canonicalizedHeader.Length);
                Array.Copy(canonicalizedDesiredProperties, 0, canonicalizedCombinedDesiredProperties, canonicalizedHeader.Length, canonicalizedDesiredProperties.Length);

                if (algorithmScheme == "ES")
                {
                    ECDsa eCDsa = signerCert.GetECDsaPublicKey();
                    return eCDsa.VerifyData(canonicalizedCombinedDesiredProperties, signatureBytes, hashAlgorithm);
                }
                else if (algorithmScheme == "RS")
                {
                    RSA rsa = signerCert.GetRSAPublicKey();
                    RSASignaturePadding rsaSignaturePadding = RSASignaturePadding.Pkcs1;
                    return rsa.VerifyData(canonicalizedCombinedDesiredProperties, signatureBytes, hashAlgorithm, rsaSignaturePadding);
                }
                else
                {
                    throw new Exception("DSA Algorithm Type not supported");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static string GetAlgorithmScheme(string algo)
        {
            try
            {
                if (algo.Length < 5)
                {
                    throw new Exception("DSA algorithm is not specific correctly.");
                }

                if (algo[0..2] == "ES" || algo[0..2] == "RS")
                {
                    return algo[0..2];
                }
                else
                {
                    throw new Exception("DSA Algorithm Type not supported");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static HashAlgorithmName GetHashAlgorithm(string algo)
        {
            try
            {
                if (algo.Length > 5)
                {
                    throw new Exception("SHA algorithm is not specific correctly");
                }
                else if (algo[2..5] == "256")
                {
                    return HashAlgorithmName.SHA256;
                }
                else if (algo[2..5] == "384")
                {
                    return HashAlgorithmName.SHA384;
                }
                else if (algo[2..5] == "512")
                {
                    return HashAlgorithmName.SHA512;
                }
                else
                {
                    throw new Exception("SHA Algorithm Type not supported");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
