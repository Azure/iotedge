// Copyright (c) Microsoft. All rights reserved.

namespace ManifestSignerClient
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    class Program
    {
        // Edit the launchSettings.json or directly add the values of the Environmental variables
        static readonly string DsaAlgorithm = Environment.GetEnvironmentVariable("DSA_ALGORITHM");
        static readonly string DeploymentManifestFilePath = Environment.GetEnvironmentVariable("DEPLOYMENT_MANIFEST_FILE_PATH");
        static readonly string SignedDeploymentManifestFilePath = Environment.GetEnvironmentVariable("SIGNED_DEPLOYMENT_MANIFEST_FILE_PATH");
        static readonly string UseTestingCA = Environment.GetEnvironmentVariable("USE_TESTING_CA");
        static readonly string DeviceRootCaPath = Environment.GetEnvironmentVariable("MANIFEST_TRUST_DEVICE_ROOT_CA_PATH");
        static readonly string SignerPrivateKeyPath = Environment.GetEnvironmentVariable("MANIFEST_TRUST_SIGNER_PRIVATE_KEY_PATH");
        static readonly string SignerCertPath = Environment.GetEnvironmentVariable("MANIFEST_TRUST_SIGNER_CERT_PATH");

        static void CheckInputAndDisplayLaunchSettings()
        {
            Console.WriteLine("Display settings from launchSettings.json");
            Console.WriteLine("================================================================================================");

            if (string.IsNullOrEmpty(DsaAlgorithm))
            {
                throw new Exception("DSA Algorithm value is empty. Please update it in launchSettings.json");
            }
            else
            {
                Console.WriteLine($"DSA Algorithm is {DsaAlgorithm}");
            }

            if (string.IsNullOrEmpty(DeploymentManifestFilePath))
            {
                throw new Exception("Deployment Manifest path is empty. Please update it in launchSettings.json");
            }
            else
            {
                Console.WriteLine($"Deployment Manifest path is {DeploymentManifestFilePath}");
            }

            if (string.IsNullOrEmpty(SignedDeploymentManifestFilePath))
            {
                throw new Exception("Signed Deployment Manifest path is empty. Please update it in launchSettings.json");
            }
            else
            {
                Console.WriteLine($"Signed Deployment Manifest path is {SignedDeploymentManifestFilePath}");
            }

            if (string.IsNullOrEmpty(UseTestingCA))
            {
                throw new Exception("Use Testing CA settings is empty. Please update it in launchSettings.json");
            }
            else
            {
                Console.WriteLine($"Use Testing CA is set to {UseTestingCA}");
            }

            if (string.IsNullOrEmpty(DeviceRootCaPath))
            {
                throw new Exception("Device Root CA path is empty. Please update it in launchSettings.json");
            }
            else
            {
                Console.WriteLine($"Device Root CA path is {DeviceRootCaPath}");
            }

            if (string.IsNullOrEmpty(SignerPrivateKeyPath))
            {
                throw new Exception("Signer Private key path is empty. Please update it in launchSettings.json");
            }
            else
            {
                Console.WriteLine($"Signer Private key path is {SignerPrivateKeyPath}");
            }

            if (string.IsNullOrEmpty(SignerCertPath))
            {
               throw new Exception("Signer Certificate path is empty. Please update it in launchSettings.json");
            }
            else
            {
                Console.WriteLine($"Signer Certificate path is {SignerCertPath}");
            }

            Console.WriteLine("================================================================================================");
        }

        static JObject GetIntegrityHeader(string SignerCertPath)
        {
            var signerCert = CertificateUtil.GetBase64CertContent(SignerCertPath);
            var signerCert1 = signerCert.Substring(0, signerCert.Length / 2);
            var signerCert2 = signerCert.Substring(signerCert.Length / 2);
            object headerObject = new
            {
                cert1 = signerCert1,
                cert2 = signerCert2
            };

            return JObject.FromObject(headerObject);
        }

        static string GetAlgorithmScheme(string DsaAlgorithm)
        {
            if (DsaAlgorithm.Length < 2)
            {
                throw new Exception("DSA algorithm is not specific correctly \n Supported Algorithm types: \n === In ECDSA === \n 1. EC256 \n 2. EC384 \n 3. EC512 \n === In RSA === \n 1. RS256 \n 2. RS384 \n 3. RS512");
            }

            if (DsaAlgorithm[0..2] == "ES" || DsaAlgorithm[0..2] == "RS")
            {
                return DsaAlgorithm[0..2];
            }
            else
            {
                throw new Exception("DSA Algorithm Type not supported \n Supported Algorithm types: \n === In ECDSA === \n 1. EC256 \n 2. EC384 \n 3. EC512 \n === In RSA === \n 1. RS256 \n 2. RS384 \n 3. RS512");
            }
        }

        private static HashAlgorithmName GetHashAlgorithm(string DsaAlgorithm)
        {
            try
            {
                if (DsaAlgorithm.Length <= 2 || DsaAlgorithm.Length > 5)
                {
                    throw new Exception("SHA algorithm is not specific correctly \n Supported Algorithm types: \n === In ECDSA === \n 1. EC256 \n 2. EC384 \n 3. EC512 \n === In RSA === \n 1. RS256 \n 2. RS384 \n 3. RS512");
                }
                else if (DsaAlgorithm[2..5] == "256")
                {
                    return HashAlgorithmName.SHA256;
                }
                else if (DsaAlgorithm[2..5] == "384")
                {
                    return HashAlgorithmName.SHA384;
                }
                else if (DsaAlgorithm[2..5] == "512")
                {
                    return HashAlgorithmName.SHA512;
                }
                else
                {
                    throw new Exception("SHA Algorithm Type not supported \n Supported Algorithm types: \n === In ECDSA === \n 1. EC256 \n 2. EC384 \n 3. EC512 \n === In RSA === \n 1. RS256 \n 2. RS384 \n 3. RS512");
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        static bool VerifyCertificate(X509Certificate2 signerCertificate, X509Certificate2 rootCertificate)
        {
            var chain = new X509Chain();
            chain.ChainPolicy.ExtraStore.Add(rootCertificate);

            if (UseTestingCA == "true")
            {
                // You can alter how the chain is built/validated. These checks are only for testing.
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            }

            // Do the validation
            var result = chain.Build(signerCertificate);

            return result;
        }

        static void Main()
        {
            // Initialize & Check if the given signer cert is issued by root CA
            try
            {
                // Check launch settings and display it
                CheckInputAndDisplayLaunchSettings();
                var signerCertificate = new X509Certificate2(SignerCertPath);
                var rootCertificate = new X509Certificate2(DeviceRootCaPath);
                var result = VerifyCertificate(signerCertificate, rootCertificate);
                if (!result)
                {
                    throw new Exception("Please provide a signer cert issued by the given root CA");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(0);
            }

            // Read the deployment manifest file and get signature of each modules's desired properties
            try
            {
                var manifestFileHandle = File.OpenText(DeploymentManifestFilePath);
                var deploymentManifestContentJson = JObject.Parse(manifestFileHandle.ReadToEnd());
                if (deploymentManifestContentJson["modulesContent"] != null)
                {
                    // Get the DSA and SHA algorithm
                    KeyValuePair<string, HashAlgorithmName> algoResult = VerifyTwinSignature.CheckIfAlgorithmIsSupported(DsaAlgorithm.ToString());

                    // Read the signer certificate and manifest version number and create integrity header object
                    var header = GetIntegrityHeader(SignerCertPath);

                    // Read each module's content and its desired properties
                    var modulesContentJson = deploymentManifestContentJson["modulesContent"];
                    JObject modulesContentJobject = JObject.Parse(modulesContentJson.ToString());

                    foreach (JProperty property in modulesContentJobject.Properties())
                    {
                        if (modulesContentJson[property.Name] != null)
                        {
                            if (modulesContentJson[property.Name]["properties.desired"] != null)
                            {
                                var modulesDesired = modulesContentJson[property.Name]["properties.desired"];
                                var moduleName = property.Name.ToString();

                                if (moduleName != "$edgeAgent" && moduleName != "$edgeHub")
                                {
                                    Console.WriteLine($"Do you want to sign the desired properties of the module - {moduleName}? - Type Y or N to continue");
                                    Console.WriteLine("!!! Important Note !!! - If the module's desired properties are signed then the module's application code has to be rewritten to verify signatures");
                                    string userSigningChoice = Console.ReadLine();
                                    if (userSigningChoice != "Y" && userSigningChoice != "y")
                                    {
                                        Console.WriteLine($"{moduleName} will not be signed");
                                        continue;
                                    }
                                }

                                Console.WriteLine($"Signing Module: {property.Name}");
                                object signature = new
                                {
                                    bytes = CertificateUtil.GetJsonSignature(algoResult.Key, algoResult.Value, modulesDesired.ToString(), header, SignerPrivateKeyPath),
                                    algorithm = DsaAlgorithm
                                };
                                deploymentManifestContentJson["modulesContent"][property.Name]["properties.desired"]["integrity"] = JObject.FromObject(new { header, signature });
                            }
                            else
                            {
                                throw new Exception($"Could not find {property.Name}'s desired properties in the manifest file");
                            }
                        }
                        else
                        {
                            throw new Exception($"Could not find {property.Name} in the manifest file");
                        }
                    }

                    using var signedDeploymentfile = File.CreateText(SignedDeploymentManifestFilePath);
                    signedDeploymentfile.Write(deploymentManifestContentJson);
                }
                else
                {
                    throw new Exception("Could not find modulesContent in the manifest file");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Environment.Exit(0);
            }
        }
    }
}
