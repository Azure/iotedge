// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Azure.Devices.Edge.Azure.Monitor.FixedSetTableUpload
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using ICSharpCode.SharpZipLib.Zip.Compression;
    using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
    using Microsoft.Extensions.Logging;
    using Org.BouncyCastle.Crypto;
    using Org.BouncyCastle.Crypto.Parameters;
    using Org.BouncyCastle.OpenSsl;
    using Microsoft.Azure.Devices.Edge.Util;
    using Org.BouncyCastle.Security;

    using Certificategenerator;

    public sealed class AzureFixedSetTable
    {
        public static readonly AzureFixedSetTable instance = new AzureFixedSetTable();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static AzureFixedSetTable()
        {
        }

        public static AzureFixedSetTable Instance
        {
            get
            {
                return instance;
            }
        }

        private X509Certificate2 cert;

        private int failurecount = 0;
        private DateTime lastFailureReportedTime = DateTime.UnixEpoch;

        /// <summary>
        /// Sends data to the InsightsMetrics table in Log Analytics
        /// <returns>
        /// True on success, false on failure.
        /// </returns>
        /// </summary>
        public async Task<bool> PostAsync(string workspaceId, string sharedKey, string content, string ArmResourceId)
        {
            Preconditions.CheckNotNull(workspaceId, "Workspace Id cannot be empty.");
            Preconditions.CheckNotNull(sharedKey, "Workspace Key cannot be empty.");
            Preconditions.CheckNotNull(content, "Fixed set table content cannot be empty.");

            try
            {
                // Lazily generate and register certificate.
                if (cert == null)
                {
                    (X509Certificate2 tempCert, (string certString, byte[] certBuf), string keyString) = CertGenerator.RegisterAgentWithOMS(workspaceId, sharedKey, Constants.DefaultLogAnalyticsWorkspaceDomainPrefixOms);
                    cert = tempCert;
                }
                using (var handler = new HttpClientHandler())
                {
                    handler.ClientCertificates.Add(cert);
                    handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                    handler.PreAuthenticate = true;
                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;

                    Uri requestUri = new Uri("https://" + workspaceId + Constants.DefaultLogAnalyticsWorkspaceDomainPrefixOds + Settings.Current.AzureDomain + "/OperationalData.svc/PostJsonDataItems");

                    using (HttpClient client = new HttpClient(handler))
                    {
                        client.DefaultRequestHeaders.Add("x-ms-date", DateTime.Now.ToString("YYYY-MM-DD'T'HH:mm:ssZ"));  // should be RFC3339 format;
                        client.DefaultRequestHeaders.Add("X-Request-ID", Guid.NewGuid().ToString("B"));  // This is host byte order instead of network byte order, but it doesn't mater here
                        client.DefaultRequestHeaders.Add("User-Agent", "IotEdgeContainerAgent/" + Constants.VersionNumber);
                        client.DefaultRequestHeaders.Add("x-ms-AzureResourceId", ArmResourceId);

                        // TODO: replace with actual version number
                        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("IotEdgeContainerAgent", Constants.VersionNumber));

                        // optionally compress content before sending
                        int contentLength;
                        HttpContent contentMsg;
                        if (Settings.Current.CompressForUpload)
                        {
                            byte[] withHeader = ZlibDeflate(Encoding.UTF8.GetBytes(content));
                            contentLength = withHeader.Length;

                            contentMsg = new ByteArrayContent(withHeader);
                            contentMsg.Headers.Add("Content-Encoding", "deflate");
                        }
                        else
                        {
                            contentMsg = new StringContent(content, Encoding.UTF8);
                            contentLength = ASCIIEncoding.Unicode.GetByteCount(content);
                        }

                        if (contentLength > 1024 * 1024)
                        {
                            LoggerUtil.Writer.LogDebug(
                                "HTTP post content greater than 1mb" + " " +
                                "Length - " + contentLength.ToString());
                        }

                        contentMsg.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                        var response = await client.PostAsync(requestUri, contentMsg).ConfigureAwait(false);
                        var responseMsg = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        LoggerUtil.Writer.LogDebug(
                            ((int)response.StatusCode).ToString() + " " +
                            response.ReasonPhrase + " " +
                            responseMsg);

                        if ((int)response.StatusCode != 200)
                        {
                            failurecount += 1;

                            if (DateTime.Now - lastFailureReportedTime > TimeSpan.FromMinutes(1))
                            {
                                LoggerUtil.Writer.LogDebug(
                                    "abnormal HTTP response code - " +
                                    "responsecode: " + ((int)response.StatusCode).ToString() + " " +
                                    "reasonphrase: " + response.ReasonPhrase + " " +
                                    "responsemsg: " + responseMsg + " " +
                                    "count: " + failurecount);
                                failurecount = 0;
                                lastFailureReportedTime = DateTime.Now;
                            }

                            // It's possible that the generated certificate is bad, maybe the module has been running for a over a month? (in which case a topology request would be needed to refresh the cert).
                            // Regen the cert on next run just to be safe.
                            cert = null;
                        }
                        return ((int)response.StatusCode) == 200;
                    }
                }
            }
            catch (Exception e)
            {
                LoggerUtil.Writer.LogError(e.Message);
                if (e.InnerException != null)
                {
                    LoggerUtil.Writer.LogError("InnerException - " + e.InnerException.Message);
                }
            }

            return false;
        }

        /// <summary>
        /// Compresses a byte array using the Zlib format
        /// <param name="input"> Byte array to compress </param>
        /// </summary>
        private static byte[] ZlibDeflate(byte[] input)
        {
            // TODO: test

            // "Deflate" compression often instead refers to a Zlib format which requies a 2 byte header and checksum (RFC 1950).
            // The C# built in deflate stream doesn't support this, so use an external library.
            // Hopefully a built-in Zlib stream will be included in .net 5 (https://github.com/dotnet/runtime/issues/2236)
            var deflater = new Deflater(5, false);
            using (var memoryStream = new MemoryStream())
            using (DeflaterOutputStream outStream = new DeflaterOutputStream(memoryStream, deflater))
            {
                outStream.IsStreamOwner = false;
                outStream.Write(input, 0, input.Length);
                outStream.Flush();
                outStream.Finish();
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Constructs a X509Certificate2 object with a private key
        /// <param name="certString"> Binary data of a X509 certificate (the base64 part of a PEM file)</param>
        /// <param name="keyString"> String contents of a PEM file containing a RSA private key </param>
        /// </summary>
        private static X509Certificate2 ReadX509CertWithKey(byte[] certBuffer, string keyString)
        {
            try
            {
                RSACryptoServiceProvider parsedKey;
                X509Certificate2 cert;

                // The built-in C# library for parsing RSA keys from byte arrays seems to have a bug and only works on linux.
                // Thus this code will only work on linux:
                //      using var rsa = RSA.Create();
                //      rsa.ImportRSAPrivateKey(keyBuffer, out _);
                // Since the key generation code has a dependancy on bouncy castle anyways, we will use it.
                // from https://gist.github.com/therightstuff/aa65356e95f8d0aae888e9f61aa29414

                // parse the private key
                PemReader pr = new PemReader(new StringReader(keyString));
                AsymmetricCipherKeyPair KeyPair = (AsymmetricCipherKeyPair)pr.ReadObject();
                RSAParameters rsaParams = DotNetUtilities.ToRSAParameters((RsaPrivateCrtKeyParameters)KeyPair.Private);

                parsedKey = new RSACryptoServiceProvider();
                parsedKey.ImportParameters(rsaParams);

                // now parse the certificate
                cert = new X509Certificate2(certBuffer);
                cert = cert.CopyWithPrivateKey(parsedKey);
                return cert;
            }
            catch (Exception e)
            {
                // log an error and exit. Modules are restarted automatically so it makes more sense to crash and restart than recover from this.
                LoggerUtil.Writer.LogCritical(e.ToString());
                Environment.Exit(1);

                // to make the code analyzer happy, otherwise not all codepaths return a value
                throw new Exception();
            }
        }
    }
}
