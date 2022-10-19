// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.Certificategenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Org.BouncyCastle.Asn1.X509;
    using Org.BouncyCastle.Crypto;
    using Org.BouncyCastle.Crypto.Generators;
    using Org.BouncyCastle.Crypto.Operators;
    using Org.BouncyCastle.Math;
    using Org.BouncyCastle.OpenSsl;
    using Org.BouncyCastle.Pkcs;
    using Org.BouncyCastle.Security;
    using Org.BouncyCastle.Utilities;
    using Org.BouncyCastle.X509;

    public static class CertGenerator
    {
        internal static class Constants
        {
            /// <summary>
            /// constants related to masking the secrets in container environment variable
            /// </summary>
            public const string DEFAULT_LOG_ANALYTICS_WORKSPACE_DOMAIN = "opinsights.azure.com";

            public const string DEFAULT_SIGNATURE_ALOGIRTHM = "SHA256WithRSA";
        }

        private static (X509Certificate2, (string, byte[]), string) CreateSelfSignedCertificate(string agentGuid, string logAnalyticsWorkspaceId)
        {
            var random = new SecureRandom();

            var certificateGenerator = new X509V3CertificateGenerator();

            var serialNumber = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(Int64.MaxValue), random);

            certificateGenerator.SetSerialNumber(serialNumber);

            var dirName = string.Format("CN={0}, CN={1}, OU=Microsoft Monitoring Agent, O=Microsoft", logAnalyticsWorkspaceId, agentGuid);

            X509Name certName = new X509Name(dirName);

            certificateGenerator.SetIssuerDN(certName);

            certificateGenerator.SetSubjectDN(certName);

            certificateGenerator.SetNotBefore(DateTime.UtcNow.Date);

            certificateGenerator.SetNotAfter(DateTime.UtcNow.Date.AddYears(1));

            const int strength = 2048;

            var keyGenerationParameters = new KeyGenerationParameters(random, strength);

            var keyPairGenerator = new RsaKeyPairGenerator();

            keyPairGenerator.Init(keyGenerationParameters);

            var subjectKeyPair = keyPairGenerator.GenerateKeyPair();

            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            // Get Private key for the Certificate
            TextWriter textWriter = new StringWriter();
            PemWriter pemWriter = new PemWriter(textWriter);
            pemWriter.WriteObject(subjectKeyPair.Private);
            pemWriter.Writer.Flush();

            string privateKeyString = textWriter.ToString();

            var issuerKeyPair = subjectKeyPair;
            var signatureFactory = new Asn1SignatureFactory(Constants.DEFAULT_SIGNATURE_ALOGIRTHM, issuerKeyPair.Private);
            var bouncyCert = certificateGenerator.Generate(signatureFactory);

            // Lets convert it to X509Certificate2
            X509Certificate2 certificate;

            Pkcs12Store store = new Pkcs12StoreBuilder().Build();

            store.SetKeyEntry($"{agentGuid}_key", new AsymmetricKeyEntry(subjectKeyPair.Private), new[] { new X509CertificateEntry(bouncyCert) });

            string exportpw = Guid.NewGuid().ToString("x");

            using (var ms = new MemoryStream())
            {
                store.Save(ms, exportpw.ToCharArray(), random);
                certificate = new X509Certificate2(ms.ToArray(), exportpw, X509KeyStorageFlags.Exportable);
            }

            // // Get the value.
            // string resultsTrue = certificate.ToString(true);

            //Get Certificate in PEM format
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(
                Convert.ToBase64String(certificate.RawData, Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----");
            string certString = builder.ToString();

            return (certificate, (certString, certificate.RawData), privateKeyString);
        }

        // Delete the certificate and key files
        private static void DeleteCertificateAndKeyFile()
        {
            File.Delete(Environment.GetEnvironmentVariable("CI_CERT_LOCATION"));
            File.Delete(Environment.GetEnvironmentVariable("CI_KEY_LOCATION"));
        }

        private static string Sign(string requestdate, string contenthash, string key)
        {
            var signatureBuilder = new StringBuilder();
            signatureBuilder.Append(requestdate);
            signatureBuilder.Append("\n");
            signatureBuilder.Append(contenthash);
            signatureBuilder.Append("\n");
            string rawsignature = signatureBuilder.ToString();

            //string rawsignature = contenthash;

            HMACSHA256 hKey = new HMACSHA256(Convert.FromBase64String(key));
            return Convert.ToBase64String(hKey.ComputeHash(Encoding.UTF8.GetBytes(rawsignature)));
        }

        public static void RegisterWithOms(X509Certificate2 cert, string AgentGuid, string logAnalyticsWorkspaceId, string logAnalyticsWorkspaceKey, string logAnalyticsWorkspaceDomainPrefixOms)
        {

            string rawCert = Convert.ToBase64String(cert.GetRawCertData()); //base64 binary
            string hostName = Dns.GetHostName();

            string date = DateTime.Now.ToString("O");

            string xmlContent = "<?xml version=\"1.0\"?>" +
                "<AgentTopologyRequest xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns=\"http://schemas.microsoft.com/WorkloadMonitoring/HealthServiceProtocol/2014/09/\">" +
                "<FullyQualfiedDomainName>"
                 + hostName
                + "</FullyQualfiedDomainName>" +
                "<EntityTypeId>"
                    + AgentGuid
                + "</EntityTypeId>" +
                "<AuthenticationCertificate>"
                  + rawCert
                + "</AuthenticationCertificate>" +
                "</AgentTopologyRequest>";

            SHA256 sha256 = SHA256.Create();

            string contentHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.ASCII.GetBytes(xmlContent)));

            string authKey = string.Format("{0}; {1}", logAnalyticsWorkspaceId, Sign(date, contentHash, logAnalyticsWorkspaceKey));


            HttpClientHandler clientHandler = new HttpClientHandler();

            clientHandler.ClientCertificates.Add(cert);

            var client = new HttpClient(clientHandler);

            string url = "https://" + logAnalyticsWorkspaceId + logAnalyticsWorkspaceDomainPrefixOms + Settings.Current.AzureDomain + "/AgentService.svc/AgentTopologyRequest";

            LoggerUtil.Writer.LogInformation("OMS endpoint Url : {0}", url);

            client.DefaultRequestHeaders.Add("x-ms-Date", date);
            client.DefaultRequestHeaders.Add("x-ms-version", "August, 2014");
            client.DefaultRequestHeaders.Add("x-ms-SHA256_Content", contentHash);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authKey);
            client.DefaultRequestHeaders.Add("user-agent", "MonitoringAgent/OneAgent");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US");

            HttpContent httpContent = new StringContent(xmlContent, Encoding.UTF8);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");

            LoggerUtil.Writer.LogInformation("sending registration request");
            Task<HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);
            LoggerUtil.Writer.LogInformation("waiting for response to registration request");
            response.Wait();
            LoggerUtil.Writer.LogInformation("registration request processed");
            LoggerUtil.Writer.LogInformation("Response result status code : {0}", response.Result.StatusCode);
            HttpContent responseContent = response.Result.Content;
            string contentString = responseContent.ReadAsStringAsync().Result;
            LoggerUtil.Writer.LogInformation("serialized response content: " + contentString);
            if (response.Result.StatusCode != HttpStatusCode.OK)
            {
                DeleteCertificateAndKeyFile();
            }
        }

        public static void RegisterWithOmsWithBasicRetryAsync(X509Certificate2 cert, string AgentGuid, string logAnalyticsWorkspaceId, string logAnalyticsWorkspaceKey, string logAnalyticsWorkspaceDomainPrefixOms)
        {
            int currentRetry = 0;

            for (; ; )
            {
                try
                {
                    RegisterWithOms(
                       cert, AgentGuid, logAnalyticsWorkspaceId, logAnalyticsWorkspaceKey, logAnalyticsWorkspaceDomainPrefixOms);

                    // Return or break.
                    break;
                }
                catch (Exception ex)
                {
                    currentRetry++;

                    // Check if the exception thrown was a transient exception
                    // based on the logic in the error detection strategy.
                    // Determine whether to retry the operation, as well as how
                    // long to wait, based on the retry strategy.
                    if (currentRetry > 3)
                    {
                        // If this isn't a transient error or we shouldn't retry,
                        // rethrow the exception.
                        LoggerUtil.Writer.LogWarning("exception occurred : {0}", ex.Message);
                        throw;
                    }
                }

                // Wait to retry the operation.
                // Consider calculating an exponential delay here and
                // using a strategy best suited for the operation and fault.
                Task.Delay(1000);
            }
        }

        public static (X509Certificate2 tempCert, (string, byte[]), string) RegisterAgentWithOMS(string logAnalyticsWorkspaceId, string logAnalyticsWorkspaceKey, string logAnalyticsWorkspaceDomainPrefixOms)
        {
            X509Certificate2 agentCert = null;
            string certString;
            byte[] certBuf;
            string keyString;

            var agentGuid = Guid.NewGuid().ToString("B");

            try
            {
                Environment.SetEnvironmentVariable("CI_AGENT_GUID", agentGuid);
            }
            catch (Exception ex)
            {
                LoggerUtil.Writer.LogError("Failed to set env variable (CI_AGENT_GUID)" + ex.Message);
            }

            try
            {
                (agentCert, (certString, certBuf), keyString) = CreateSelfSignedCertificate(agentGuid, logAnalyticsWorkspaceId);

                if (agentCert == null)
                {
                    throw new Exception($"creating self-signed certificate failed for agentGuid : {agentGuid} and workspace: {logAnalyticsWorkspaceId}");
                }

                LoggerUtil.Writer.LogInformation($"Successfully created self-signed certificate  for agentGuid : {agentGuid} and workspace: {logAnalyticsWorkspaceId}");

                RegisterWithOmsWithBasicRetryAsync(agentCert, agentGuid,
                    logAnalyticsWorkspaceId,
                    logAnalyticsWorkspaceKey,
                    logAnalyticsWorkspaceDomainPrefixOms);
            }
            catch (Exception ex)
            {
                LoggerUtil.Writer.LogError("Registering agent with OMS failed (are the Log Analytics Workspace ID and Key correct?) : {0}", ex.Message);

                LoggerUtil.Writer.LogCritical(ex.ToString());
                Environment.Exit(1);

                // to make the code analyzer happy
                throw new Exception();
            }

            return (agentCert, (certString, certBuf), keyString);
        }
    }
}
