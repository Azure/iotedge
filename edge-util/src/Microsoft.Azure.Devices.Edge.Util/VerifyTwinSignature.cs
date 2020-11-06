// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
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
                    throw new TwinSignatureAlgorithmException("DSA Algorithm Type not supported");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static KeyValuePair<string, HashAlgorithmName> CheckIfAlgorithmIsSupported(string dsaAlgorithm)
        {
            SupportedTwinSigningAlgorithm result;
            if (Enum.TryParse(dsaAlgorithm, true, out result))
            {
                GetAlgoType.TryGetValue(result, out KeyValuePair<string, HashAlgorithmName> val);
                return val;
            }
            else
            {
                throw new TwinSignatureAlgorithmException("DSA Algorithm Type not supported");
            }
        }

        public enum SupportedTwinSigningAlgorithm
        {
            Es256,
            Es384,
            Es512,
            Rs256,
            Rs384,
            Rs512,
        }

        static readonly Dictionary<SupportedTwinSigningAlgorithm, KeyValuePair<string, HashAlgorithmName>> GetAlgoType
           = new Dictionary<SupportedTwinSigningAlgorithm, KeyValuePair<string, HashAlgorithmName>>
           {
               { SupportedTwinSigningAlgorithm.Es256, new KeyValuePair<string, HashAlgorithmName>("ES", HashAlgorithmName.SHA256) },
               { SupportedTwinSigningAlgorithm.Es384, new KeyValuePair<string, HashAlgorithmName>("ES", HashAlgorithmName.SHA384) },
               { SupportedTwinSigningAlgorithm.Es512, new KeyValuePair<string, HashAlgorithmName>("ES", HashAlgorithmName.SHA512) },
               { SupportedTwinSigningAlgorithm.Rs256, new KeyValuePair<string, HashAlgorithmName>("RS", HashAlgorithmName.SHA256) },
               { SupportedTwinSigningAlgorithm.Rs384, new KeyValuePair<string, HashAlgorithmName>("RS", HashAlgorithmName.SHA384) },
               { SupportedTwinSigningAlgorithm.Rs512, new KeyValuePair<string, HashAlgorithmName>("RS", HashAlgorithmName.SHA512) },
           };
    }
}
