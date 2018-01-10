// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using System.Collections.Concurrent;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using System;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;

    class DeviceClientCache
    {
        readonly ConcurrentDictionary<string, DeviceClient> clients = new ConcurrentDictionary<string, DeviceClient>();

        // Private constructor to ensure single instance
        DeviceClientCache()
        {
        }

        public static DeviceClientCache Instance { get; } = new DeviceClientCache();

        public DeviceClient GetOrCreate(string connectionString)
        {
            return this.clients.GetOrAdd(
                connectionString,
                client => this.CreateDeviceClient(connectionString));
        }

        DeviceClient CreateDeviceClient(string connectionString)
        {
            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);

            // Suppress cert validation on Windows for now
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            }
            else
            {
                // get CA certificate
                string certPath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");

                var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);  // On Linux only root store worked
                store.Open(OpenFlags.ReadWrite);
                store.Add(new X509Certificate2(X509Certificate.CreateFromCertFile(certPath)));
                store.Close();
            }

            ITransportSettings[] settings = { mqttSetting };
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(connectionString, settings);

            deviceClient.ProductInfo = "Microsoft.Azure.Devices.Edge.Functions.Binding";
            return deviceClient;
        }
    }
}
