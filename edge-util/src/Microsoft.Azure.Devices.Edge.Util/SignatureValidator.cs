// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using Org.Webpki.JsonCanonicalizer;
    public class SignatureValidator
    {
        public static bool VerifySignature(string payload, string header, byte[] signatureBytes, X509Certificate2 signerCert, string algorithmScheme, HashAlgorithmName hashAlgorithm)
        {
            try
            {
                byte[] canonicalizedFinalData = GetCanonicalizedInputBytes(payload, header);

                if (algorithmScheme == "ES")
                {
                    ECDsa eCDsa = signerCert.GetECDsaPublicKey();
                    return eCDsa.VerifyData(canonicalizedFinalData, signatureBytes, hashAlgorithm);
                }
                else if (algorithmScheme == "RS")
                {
                    RSA rsa = signerCert.GetRSAPublicKey();
                    RSASignaturePadding rsaSignaturePadding = RSASignaturePadding.Pkcs1;
                    return rsa.VerifyData(canonicalizedFinalData, signatureBytes, hashAlgorithm, rsaSignaturePadding);
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

        public static byte[] GetCanonicalizedInputBytes(string payload, string header)
        {
            // Json Canonicalization of JSON data needs to be done as we separate the signature from the payload
            // First Canonicalization of payload and extract its byte data
            JsonCanonicalizer canonicalizerPayload = new JsonCanonicalizer(payload);
            byte[] canonicalizedPayload = canonicalizerPayload.GetEncodedUTF8();

            // Next Canonicalization of the header part and extract its byte data
            JsonCanonicalizer canonicalizerHeader = new JsonCanonicalizer(header);
            byte[] canonicalizedHeader = canonicalizerHeader.GetEncodedUTF8();

            // Create a new byte array with the combination of the payload and the header
            byte[] canonicalizedFinalData = new byte[canonicalizedHeader.Length + canonicalizedPayload.Length];

            // Copy the header and payload into the final array
            Array.Copy(canonicalizedHeader, 0, canonicalizedFinalData, 0, canonicalizedHeader.Length);
            Array.Copy(canonicalizedPayload, 0, canonicalizedFinalData, canonicalizedHeader.Length, canonicalizedPayload.Length);

            return canonicalizedFinalData;
        }

        public static KeyValuePair<string, HashAlgorithmName> ParseAlgorithm(string dsaAlgorithm)
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
