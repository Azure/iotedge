// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Client.Samples
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Security;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

    class Program
    {
        /* 1) Obtain the IoT Hub hostname and device id for your downstream device.
              Update the IOTHUB_HOSTNAME and DEVICE_ID in the Properties/launchSettings.json file.
           2) Obtain the Edge device hostname: The edge device hostname is the hostname set in
              the config.yaml of the Edge device to which this sample will connect to.
              Update the IOTEDGE_GATEWAY_HOSTNAME in the Properties/launchSettings.json file.
           3) Obtain the trusted CA certificate file required to trust the Edge gateway.
              In the docs this would be the azure-iot-test-only.root.ca.cert.pem.
              Update IOTEDGE_TRUSTED_CA_CERTIFICATE_PEM_PATH to point to this file.
           4) Optionally, update DEVICE_CLIENT_PROTOCOL to indicate the choice of protocol to use.
              Options are Mqtt, MqttWs, Amqp, AmqpWS. Default is Mqtt.
           5) Optionally, update MESSAGE_COUNT to indicate the number of telemetry messages to send
              to the Edge gateway. Default is 10. */

        const int MaxCertImportRetryCount = 10;

        const int TEMPERATURE_THRESHOLD = 30;
        static readonly string IothubHostname = Environment.GetEnvironmentVariable("IOTHUB_HOSTNAME");
        static readonly string DownstreamDeviceId = Environment.GetEnvironmentVariable("DEVICE_ID");
        static readonly string IotEdgeGatewayHostname = Environment.GetEnvironmentVariable("IOTEDGE_GATEWAY_HOSTNAME");
        static readonly string DeviceIdentityCertPath = Environment.GetEnvironmentVariable("DEVICE_IDENTITY_X509_CERTIFICATE_PEM_PATH");
        static readonly string DeviceIdentityPrivateKeyPath = Environment.GetEnvironmentVariable("DEVICE_IDENTITY_X509_CERTIFICATE_KEY_PEM_PATH");
        static readonly string TrustedCACertPath = Environment.GetEnvironmentVariable("IOTEDGE_TRUSTED_CA_CERTIFICATE_PEM_PATH");
        static readonly string ClientTransportType = Environment.GetEnvironmentVariable("DEVICE_CLIENT_PROTOCOL");
        static readonly string MessageCountEnv = Environment.GetEnvironmentVariable("MESSAGE_COUNT");
        static int messageCount = 10;

        private static Oid oidRsaEncryption = Oid.FromFriendlyName("RSA", OidGroup.All);
        private static Oid oidEcPublicKey = Oid.FromFriendlyName("ECC", OidGroup.All);

        public static IEnumerable<X509Certificate2> GetCertificatesFromPem(IEnumerable<string> rawPemCerts) =>
            rawPemCerts
                .Select(c => Encoding.UTF8.GetBytes(c))
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

        internal static (X509Certificate2, IEnumerable<X509Certificate2>) ParseCertificateAndKey(string certificateWithChain, string privateKey)
        {
            IEnumerable<string> pemCerts = ParsePemCerts(certificateWithChain);

            if (pemCerts.FirstOrDefault() == null)
            {
                throw new InvalidOperationException("Certificate is required");
            }

            IEnumerable<X509Certificate2> certsChain = GetCertificatesFromPem(pemCerts.Skip(1));

            var certWithNoKey = new X509Certificate2(Encoding.UTF8.GetBytes(pemCerts.First()));
            var certWithPrivateKey = AttachPrivateKey(certWithNoKey, privateKey);

            return (certWithPrivateKey, certsChain);
        }

        static (X509Certificate2 Certificate, IEnumerable<X509Certificate2> CertificateChain) GetClientCertificateAndChainFromFile(string certWithChainFilePath, string privateKeyFilePath)
        {
            string cert, privateKey;

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

        static ITransportSettings[] GetTransport(string protocol)
        {
            TransportType transportType = TransportType.Mqtt_Tcp_Only;
            ITransportSettings[] transportSettings = new ITransportSettings[1];

            if (string.Equals("Mqtt", protocol, StringComparison.OrdinalIgnoreCase))
            {
                transportType = TransportType.Mqtt_Tcp_Only;
            }
            else if (string.Equals("MqttWs", protocol, StringComparison.OrdinalIgnoreCase))
            {
                transportType = TransportType.Mqtt_WebSocket_Only;
            }
            else if (string.Equals("Amqp", protocol, StringComparison.OrdinalIgnoreCase))
            {
                transportType = TransportType.Amqp_Tcp_Only;
            }
            else if (string.Equals("AmqpWs", protocol, StringComparison.OrdinalIgnoreCase))
            {
                transportType = TransportType.Amqp_WebSocket_Only;
            }
            else
            {
                throw new ArgumentException("Invalid protocol");
            }

            X509Certificate2 trustedCACert = GetTrustedCACertFromFile(TrustedCACertPath);
            RemoteCertificateValidationCallback certificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => CustomCertificateValidator.ValidateCertificate(trustedCACert, (X509Certificate2)certificate, chain, sslPolicyErrors);
            if (transportType == TransportType.Amqp_Tcp_Only || transportType == TransportType.Amqp_WebSocket_Only)
            {
                transportSettings[0] = new AmqpTransportSettings(transportType);
                AmqpTransportSettings amqpTransportSettings = (AmqpTransportSettings)transportSettings[0];
                amqpTransportSettings.RemoteCertificateValidationCallback = certificateValidationCallback;
            }
            else
            {
                transportSettings[0] = new MqttTransportSettings(transportType);
                MqttTransportSettings mqttTransportSettings = (MqttTransportSettings)transportSettings[0];
                mqttTransportSettings.RemoteCertificateValidationCallback = certificateValidationCallback;
            }

            return transportSettings;
        }

        static X509Certificate2 AttachPrivateKey(X509Certificate2 certificate, string pemEncodedKey)
        {
            int retryCount = 0;
            while (retryCount++ < MaxCertImportRetryCount)
            {
                string pkcs8Label = "PRIVATE KEY";
                string rsaLabel = "RSA PRIVATE KEY";
                string ecLabel = "EC PRIVATE KEY";

                var keyAlgorithm = certificate.GetKeyAlgorithm();

                bool isPkcs8 = pemEncodedKey.IndexOf(Header(pkcs8Label)) >= 0;

                X509Certificate2 result = null;

                try
                {
                    if (oidRsaEncryption.Value == keyAlgorithm)
                    {
                        var decodedKey = UnwrapPrivateKey(pemEncodedKey, isPkcs8 ? pkcs8Label : rsaLabel);
                        var key = RSA.Create();

                        if (isPkcs8)
                        {
                            key.ImportPkcs8PrivateKey(decodedKey, out _);
                        }
                        else
                        {
                            key.ImportRSAPrivateKey(decodedKey, out _);
                        }

                        result = certificate.CopyWithPrivateKey(key);
                    }
                    else if (oidEcPublicKey.Value == keyAlgorithm)
                    {
                        var decodedKey = UnwrapPrivateKey(pemEncodedKey, isPkcs8 ? pkcs8Label : ecLabel);
                        var key = ECDsa.Create();

                        if (isPkcs8)
                        {
                            key.ImportPkcs8PrivateKey(decodedKey, out _);
                        }
                        else
                        {
                            key.ImportECPrivateKey(decodedKey, out _);
                        }

                        result = certificate.CopyWithPrivateKey(key);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Cannot import private key", ex);
                }

                if (result == null)
                {
                    throw new InvalidOperationException($"Cannot use certificate, not supported key algorithm: ${keyAlgorithm}");
                }

                try
                {
                    // Reading the private key to see if the import was successful. On Windows it fails sometimes,
                    // the root cause is unknown.
                    if (result.HasPrivateKey)
                    {
                        if (oidEcPublicKey.Value == keyAlgorithm)
                        {
                            _ = result.GetECDsaPrivateKey();
                        }
                        else
                        {
                            _ = result.GetRSAPrivateKey();
                        }

                        return result;
                    }
                }
                catch
                {
                    // swallow
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            throw new InvalidOperationException("Cannot import certificate with private key, giving up");
        }

        static byte[] UnwrapPrivateKey(string pemEncodedKey, string algoLabel)
        {
            var headerIndex = pemEncodedKey.IndexOf(Header(algoLabel));
            var footerIndex = pemEncodedKey.IndexOf(Footer(algoLabel));

            if (headerIndex < 0 || footerIndex < 0)
            {
                throw new InvalidOperationException($"Certificate key algorithm indicates {algoLabel}, but cannot unwrap key - headers not found");
            }

            byte[] decodedKey;

            try
            {
                var dataIndex = headerIndex + Header(algoLabel).Length;
                decodedKey = Convert.FromBase64String(pemEncodedKey.Substring(dataIndex, footerIndex - dataIndex));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Cannot decode private key: base64 decoding failed after removing headers", ex);
            }

            return decodedKey;
        }

        static string Header(string label) => $"-----BEGIN {label}-----";
        static string Footer(string label) => $"-----END {label}-----";

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
            if (string.IsNullOrEmpty(IothubHostname))
            {
                throw new ArgumentException("IoT Hub hostname cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(DownstreamDeviceId))
            {
                throw new ArgumentException("Downstream device id cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(IotEdgeGatewayHostname))
            {
                throw new ArgumentException("IoT Edge gateway hostname cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(DeviceIdentityCertPath) || !File.Exists(DeviceIdentityCertPath))
            {
                throw new ArgumentException($"Downstream device identity certificate path is invalid");
            }

            if (string.IsNullOrWhiteSpace(DeviceIdentityPrivateKeyPath) || !File.Exists(DeviceIdentityPrivateKeyPath))
            {
                throw new ArgumentException($"Downstream device identity private key path is invalid");
            }

            if (!string.IsNullOrWhiteSpace(MessageCountEnv))
            {
                if (!int.TryParse(MessageCountEnv, out messageCount))
                {
                    Console.WriteLine("Invalid number of messages in env variable MESSAGE_COUNT. MESSAGE_COUNT set to {0}\n", messageCount);
                }
            }

            if (string.IsNullOrWhiteSpace(ClientTransportType))
            {
                throw new ArgumentException("Device client protocol cannot be null or empty");
            }

            X509Certificate2 trustedCACert = GetTrustedCACertFromFile(TrustedCACertPath);
            InstallCACert(trustedCACert);

            Console.WriteLine("Creating device client using identity certificate...\n");

            var (cert, certChain) = GetClientCertificateAndChainFromFile(DeviceIdentityCertPath, DeviceIdentityPrivateKeyPath);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //Windows does not natively support PEM files for TLS connections due to the platform's lack of support for ephemeral keys. 
                //Hence need to convert the certificate to PFX format.
                cert = new X509Certificate2(cert.Export(X509ContentType.Pfx));
            }
            InstallChainCertificates(certChain);

            ITransportSettings[] transportSettings = GetTransport(ClientTransportType);
            var auth = new DeviceAuthenticationWithX509Certificate(DownstreamDeviceId, cert);
            DeviceClient deviceClient = DeviceClient.Create(IothubHostname, IotEdgeGatewayHostname, auth, transportSettings);

            if (deviceClient == null)
            {
                Console.WriteLine("Failed to create DeviceClient!");
            }
            else
            {
                SendEvents(deviceClient, messageCount).Wait();
            }

            Console.WriteLine("Exiting!\n");
        }

        static void InstallChainCertificates(IEnumerable<X509Certificate2> certificateChain)
        {
            string message;
            if (certificateChain != null)
            {
                X509Certificate2[] certs = certificateChain.ToArray();
                message = $"Found intermediate certificates: {string.Join(",", certs.Select(c => $"[{c.Subject}:{c.GetExpirationDateString()}]"))}";

                InstallCerts(
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StoreName.CertificateAuthority : StoreName.Root,
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
        static void InstallCACert(X509Certificate2 trustedCACert)
        {
            Console.WriteLine("User configured CA certificate path: {0}", TrustedCACertPath);
            Console.WriteLine("Attempting to install CA certificate: {0}", TrustedCACertPath);
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(trustedCACert);
            Console.WriteLine("Successfully added certificate: {0}", TrustedCACertPath);
            store.Close();
        }

        static X509Certificate2 GetTrustedCACertFromFile(string trustedCACertPath)
        {
            if (string.IsNullOrWhiteSpace(TrustedCACertPath) || !File.Exists(TrustedCACertPath))
            {
                Console.WriteLine("Invalid certificate file: {0}", TrustedCACertPath);
                throw new InvalidOperationException("Invalid certificate file.");
            }

            return new X509Certificate2(System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromCertFile(TrustedCACertPath));
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
