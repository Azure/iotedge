// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Azure.Devices.Client;

namespace Microsoft.Azure.Devices.Client.Samples
{
    class Program
    {

        // 1) Obtain the connection string for your downstream device and to it
        //    append this string GatewayHostName=<edge device hostname>;
        // 2) The edge device hostname is the hostname set in the config.yaml of the Edge device
        //    to which this sample will connect to.
        //
        // The resulting string should look like the following
        //  "HostName=<iothub_host_name>;DeviceId=<device_id>;SharedAccessKey=<device_key>;GatewayHostName=<edge device hostname>"
        //
        // Either set the DEVICE_CONNECTION_STRING environment variable with this connection string
        // or set it in the Properties/launchSettings.json.
        private static string DeviceConnectionString = Environment.GetEnvironmentVariable("DEVICE_CONNECTION_STRING");
        private static int MESSAGE_COUNT = 10;
        private const int TEMPERATURE_THRESHOLD = 30;
        private static float temperature;
        private static float humidity;

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
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(DeviceConnectionString);

            if (deviceClient == null)
            {
                Console.WriteLine("Failed to create DeviceClient!");
            }
            else
            {
                SendEvents(deviceClient).Wait();
            }

            Console.WriteLine("Exiting!\n");
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
        static async Task SendEvents(DeviceClient deviceClient)
        {
            string dataBuffer;
            Random rnd = new Random();
            Console.WriteLine("Edge downstream device attempting to send {0} messages to Edge Hub...\n", MESSAGE_COUNT);

            for (int count = 0; count < MESSAGE_COUNT; count++)
            {
                temperature = rnd.Next(20, 35);
                humidity = rnd.Next(60, 80);
                dataBuffer = string.Format(new CultureInfo("en-US"), "{{MyFirstDownstreamDevice \"messageId\":{0},\"temperature\":{1},\"humidity\":{2}}}", count, temperature, humidity);
                Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                eventMessage.Properties.Add("temperatureAlert", (temperature > TEMPERATURE_THRESHOLD) ? "true" : "false");
                Console.WriteLine("\t{0}> Sending message: {1}, Data: [{2}]", DateTime.Now.ToLocalTime(), count, dataBuffer);

                await deviceClient.SendEventAsync(eventMessage).ConfigureAwait(false);
            }
        }
    }
}
