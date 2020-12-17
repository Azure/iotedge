// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class SignatureValidatorTest
    {
        [Theory]
        [MemberData(nameof(InputSignatureData))]
        public static void VerifySignatureTest(bool expectedResult, string payload, string header, byte[] signatureBytes, X509Certificate2 signerCert, string algorithmScheme, HashAlgorithmName hashAlgorithm)
        {
            var actualResult = SignatureValidator.VerifySignature(payload, header, signatureBytes, signerCert, algorithmScheme, hashAlgorithm);
            Assert.Equal(actualResult, expectedResult);
        }

        [Theory]
        [MemberData(nameof(InputAlgorithm))]
        public static void ParseAlgorithmTest(string algorithm,  bool hasParseExcpetion, string expectedAlgoScheme, HashAlgorithmName expectedHashAlgo )
        {
            if (!hasParseExcpetion)
            {
                KeyValuePair<string, HashAlgorithmName> actualResult = SignatureValidator.ParseAlgorithm(algorithm);
                KeyValuePair<string, HashAlgorithmName> expectedResult = new KeyValuePair<string, HashAlgorithmName>(expectedAlgoScheme, expectedHashAlgo);
                Assert.Equal(actualResult, expectedResult);
            }
            else
            {
                Assert.Throws<TwinSignatureAlgorithmException>(() => SignatureValidator.ParseAlgorithm(algorithm));
            }
        }

        public static IEnumerable<object[]> InputSignatureData()
        {
            yield return new object[] { "ES256", "false", "ES", HashAlgorithmName.SHA256 };
            yield return new object[] { "ES384", "false", "ES", HashAlgorithmName.SHA384 };
            yield return new object[] { "ES512", "false", "ES", HashAlgorithmName.SHA512 };
            yield return new object[] { "RS256", "false", "RS", HashAlgorithmName.SHA256 };
            yield return new object[] { "RS384", "false", "RS", HashAlgorithmName.SHA384 };
            yield return new object[] { "RS512", "false", "RS", HashAlgorithmName.SHA512 };
            yield return new object[] { "SS256", "true", "ES", HashAlgorithmName.SHA256 };
            yield return new object[] { "wx", "true", "ES", HashAlgorithmName.SHA256 };
            yield return new object[] { string.Empty, "true", "ES", HashAlgorithmName.SHA256 };
        }

        public static IEnumerable<object[]> InputAlgorithm()
        {
            yield return new object[] { "payload", "header", "ES", HashAlgorithmName.SHA256 };
        }
    }
}
