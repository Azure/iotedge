// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;


using Microsoft.Azure.Devices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Azure.Devices.Client.Samples
{
    class Program
    {

        // 1) Obtain the connection string for your downstream device and to it
        //    append it with this string: GatewayHostName=<edge device hostname>;
        // 2) The edge device hostname is the hostname set in the config.yaml of the Edge device
        //    to which this sample will connect to.
        //
        // The resulting string should have this format:
        //  "HostName=<iothub_host_name>;DeviceId=<device_id>;SharedAccessKey=<device_key>;GatewayHostName=<edge device hostname>"
        //
        // Either set the DEVICE_CONNECTION_STRING environment variable with this connection string
        // or set it in the Properties/launchSettings.json.
        private static readonly string deviceConnectionString = Environment.GetEnvironmentVariable("DEVICE_CONNECTION_STRING");
        private static readonly string deviceCertPfxPath = Environment.GetEnvironmentVariable("DEVICE_X509_CERTIFICATE_PFX_PATH");
        private static readonly string deviceCertPfxPasswd = Environment.GetEnvironmentVariable("DEVICE_X509_CERTIFICATE_PFX_PASSWORD");
        private static int MESSAGE_COUNT = 10;
        private const int TEMPERATURE_THRESHOLD = 30;

        public static IEnumerable<X509Certificate2> GetCertificatesFromPem(IEnumerable<string> rawPemCerts) =>
            rawPemCerts
                .Select(c => System.Text.Encoding.UTF8.GetBytes(c))
                .Select(c => new X509Certificate2(c))
                .ToList();

        public static IList<string> ParsePemCerts(string pemCerts)
        {
            if (string.IsNullOrEmpty(pemCerts))
            {
                throw new InvalidOperationException("Trusted certificates can not be null or empty.");
            }

            // Extract each certificate's string. The final string from the split will either be empty
            // or a non-certificate entry, so it is dropped.
            string delimiter = "-----END CERTIFICATE-----";
            string[] rawCerts = pemCerts.Split(new[] { delimiter }, StringSplitOptions.None);
            return rawCerts
                .Take(rawCerts.Count() - 1) // Drop the invalid entry
                .Select(c => $"{c}{delimiter}")
                .ToList(); // Re-add the certificate end-marker which was removed by split
        }

        public static (X509Certificate2 Certificate, IEnumerable<X509Certificate2> CertificateChain) GetClientCertificateAndChainFromFile(string certWithChainFilePath, string privateKeyFilePath)
        {
            string cert, privateKey;

            if (string.IsNullOrWhiteSpace(certWithChainFilePath) || !File.Exists(certWithChainFilePath))
            {
                throw new ArgumentException($"'{certWithChainFilePath}' is not a path to a server certificate file");
            }

            if (string.IsNullOrWhiteSpace(privateKeyFilePath) || !File.Exists(privateKeyFilePath))
            {
                throw new ArgumentException($"'{privateKeyFilePath}' is not a path to a private key file");
            }

            using (var sr = new StreamReader(certWithChainFilePath))
            {
                cert = sr.ReadToEnd();
            }

            using (var sr = new StreamReader(privateKeyFilePath))
            {
                privateKey = sr.ReadToEnd();
            }

            return ParseCertificateAndKey(cert, privateKey);
        }

        internal static (X509Certificate2, IEnumerable<X509Certificate2>) ParseCertificateAndKey(string certificateWithChain, string privateKey)
        {
            IEnumerable<string> pemCerts = ParsePemCerts(certificateWithChain);

            if (pemCerts.FirstOrDefault() == null)
            {
                throw new InvalidOperationException("Certificate is required");
            }

            IEnumerable<X509Certificate2> certsChain = GetCertificatesFromPem(pemCerts.Skip(1));

            Pkcs12Store store = new Pkcs12StoreBuilder().Build();
            IList<X509CertificateEntry> chain = new List<X509CertificateEntry>();

            // note: the seperator between the certificate and private key is added for safety to delinate the cert and key boundary
            var sr = new StringReader(pemCerts.First() + "\r\n" + privateKey);
            var pemReader = new PemReader(sr);

            RsaPrivateCrtKeyParameters keyParams = null;
            object certObject = pemReader.ReadObject();
            while (certObject != null)
            {
                if (certObject is Org.BouncyCastle.X509.X509Certificate x509Cert)
                {
                    chain.Add(new X509CertificateEntry(x509Cert));
                }
                // when processing certificates generated via openssl certObject type is of AsymmetricCipherKeyPair
                if (certObject is AsymmetricCipherKeyPair)
                {
                    certObject = ((AsymmetricCipherKeyPair)certObject).Private;
                }
                if (certObject is RsaPrivateCrtKeyParameters)
                {
                    keyParams = ((RsaPrivateCrtKeyParameters)certObject);
                }

                certObject = pemReader.ReadObject();
            }

            if (keyParams == null)
            {
                throw new InvalidOperationException("Private key is required");
            }

            store.SetKeyEntry("AzureIoTClient", new AsymmetricKeyEntry(keyParams), chain.ToArray());
            using (var p12File = new MemoryStream())
            {
                store.Save(p12File, new char[] { }, new SecureRandom());

                var cert = new X509Certificate2(p12File.ToArray());
                return (cert, certsChain);
            }
        }


        /// <summary>
        /// First install any CA certificate provided by the user to connect to the Edge device.
        /// Next attempt to connect to the Edge device and send it MESSAGE_COUNT
        /// number of telemetry data messages.
        ///
        /// Note: Either set the MESSAGE_COUNT environment variable with the number of
        /// messages to be sent to the IoT Edge runtime or set it in the launchSettings.json.
        /// </summary>
        static void Main()
        {
            InstallCACert();

            try
            {
                string messageCountEnv = Environment.GetEnvironmentVariable("MESSAGE_COUNT");
                if (!string.IsNullOrWhiteSpace(messageCountEnv))
                {
                    MESSAGE_COUNT = Int32.Parse(messageCountEnv, NumberStyles.None, new CultureInfo("en-US"));
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Invalid number of messages in env variable DEVICE_MESSAGE_COUNT. MESSAGE_COUNT set to {0}\n", MESSAGE_COUNT);
            }

            Console.WriteLine("Creating device client from connection string\n");

            DeviceClient deviceClient = null;
            if (String.IsNullOrEmpty(deviceCertPfxPath))
            {
                deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);
            }
            else
            {
                string deviceId = Environment.GetEnvironmentVariable("DEVICE_ID");
                string certwithChainPem = Environment.GetEnvironmentVariable("DEVICE_X509_CERTIFICATE_PEM_PATH");
                string privateKeyPemPath = Environment.GetEnvironmentVariable("DEVICE_X509_CERTIFICATE_KEY_PEM_PATH");
                var (cert, certChain) = GetClientCertificateAndChainFromFile(certwithChainPem, privateKeyPemPath);
                InstallChainCertificates(certChain);
                var auth = new DeviceAuthenticationWithX509Certificate(deviceId, cert);
                deviceClient = DeviceClient.Create(deviceConnectionString, auth, TransportType.Amqp_Tcp_Only);
            }

            if (deviceClient == null)
            {
                Console.WriteLine("Failed to create DeviceClient!");
            }
            else
            {
                SendEvents(deviceClient, MESSAGE_COUNT).Wait();
            }

            Console.WriteLine("Exiting!\n");
        }

        public static void InstallCerts(StoreName name, StoreLocation location, IEnumerable<X509Certificate2> certs)
        {
            List<X509Certificate2> certsList = certs.ToList();
            using (var store = new X509Store(name, location))
            {
                store.Open(OpenFlags.ReadWrite);
                foreach (X509Certificate2 cert in certsList)
                {
                    store.Add(cert);
                }
            }
        }

        static void InstallChainCertificates(IEnumerable<X509Certificate2> certificateChain)
        {
            string message;
            if (certificateChain != null)
            {
                X509Certificate2[] certs = certificateChain.ToArray();
                message = $"Found intermediate certificates: {string.Join(",", certs.Select(c => $"[{c.Subject}:{c.GetExpirationDateString()}]"))}";

                InstallCerts(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StoreName.CertificateAuthority : StoreName.Root,
                             StoreLocation.CurrentUser,
                             certs);
            }
            else
            {
                message = "Unable to find intermediate certificates.";
            }

            Console.WriteLine($"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.InvariantCulture)}] {message}");
        }

        /// <summary>
        /// Add certificate in local cert store for use by downstream device
        /// client for secure connection to IoT Edge runtime.
        ///
        ///    Note: On Windows machines, if you have not run this from an Administrator prompt,
        ///    a prompt will likely come up to confirm the installation of the certificate.
        ///    This usually happens the first time a certificate will be installed.
        /// </summary>
        static void InstallCACert()
        {
            string certPath = Environment.GetEnvironmentVariable("CA_CERTIFICATE_PATH");
            if (!string.IsNullOrWhiteSpace(certPath))
            {
                Console.WriteLine("User configured CA certificate path: {0}", certPath);
                if (!File.Exists(certPath))
                {
                    // cannot proceed further without a proper cert file
                    Console.WriteLine("Invalid certificate file: {0}", certPath);
                    throw new InvalidOperationException("Invalid certificate file.");
                }
                else
                {
                    Console.WriteLine("Attempting to install CA certificate: {0}", certPath);
                    X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                    store.Open(OpenFlags.ReadWrite);
                    store.Add(new X509Certificate2(X509Certificate2.CreateFromCertFile(certPath)));
                    Console.WriteLine("Successfully added certificate: {0}", certPath);
                    store.Close();
                }
            }
            else
            {
                Console.WriteLine("CA_CERTIFICATE_PATH was not set or null, not installing any CA certificate");
            }
        }

        /// <summary>
        /// Send telemetry data, (random temperature and humidity data samples).
        /// to the IoT Edge runtime. The number of messages to be sent is determined
        /// by environment variable MESSAGE_COUNT.
        /// </summary>
        static async Task SendEvents(DeviceClient deviceClient, int messageCount)
        {
            string dataBuffer;
            Random rnd = new Random();
            Console.WriteLine("Edge downstream device attempting to send {0} messages to Edge Hub...\n", messageCount);

            for (int count = 0; count < messageCount; count++)
            {
                float temperature = rnd.Next(20, 35);
                float humidity = rnd.Next(60, 80);
                dataBuffer = string.Format(new CultureInfo("en-US"), "{{MyFirstDownstreamDevice \"messageId\":{0},\"temperature\":{1},\"humidity\":{2}}}", count, temperature, humidity);
                Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                eventMessage.Properties.Add("temperatureAlert", (temperature > TEMPERATURE_THRESHOLD) ? "true" : "false");
                Console.WriteLine("\t{0}> Sending message: {1}, Data: [{2}]", DateTime.Now.ToLocalTime(), count, dataBuffer);

                await deviceClient.SendEventAsync(eventMessage).ConfigureAwait(false);
            }
        }
    }
}
