// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using System.Collections.Concurrent;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

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
            var mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only)
            {
                // TODO: SECURITY WARNING !!! Please remove this code after Edge Hub is not using self signed certificates !!!
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            };
            ITransportSettings[] settings = { mqttSetting };

            return DeviceClient.CreateFromConnectionString(connectionString, settings);
        }
    }
}
