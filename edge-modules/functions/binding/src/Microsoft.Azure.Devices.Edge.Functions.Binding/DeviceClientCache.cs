// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using System.Collections.Concurrent;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using System;
    using System.Security.Cryptography.X509Certificates;

    class DeviceClientCache
    {
        readonly static DeviceClientCache instance = new DeviceClientCache();
        readonly ConcurrentDictionary<string, DeviceClient> clients = new ConcurrentDictionary<string, DeviceClient>();

        // Private constructor to ensure single instance
        DeviceClientCache()
        {
        }

        public static DeviceClientCache Instance => instance;

        public DeviceClient GetOrCreate(string connectionString)
        {
            return this.clients.GetOrAdd(
                connectionString,
                client => CreateDeviceClient(connectionString));
        }

        DeviceClient CreateDeviceClient(string connectionString)
        {
            // get CA certificate
            string certPath = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");

            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);  // On Linux only root store worked
            store.Open(OpenFlags.ReadWrite);
            store.Add(new X509Certificate2(X509Certificate2.CreateFromCertFile(certPath)));
            store.Close();

            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            return DeviceClient.CreateFromConnectionString(connectionString, settings);
        }
    }
}
