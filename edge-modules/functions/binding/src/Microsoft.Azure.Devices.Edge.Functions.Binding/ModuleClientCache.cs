// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using System;
    using System.Collections.Concurrent;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

    class ModuleClientCache
    {
        readonly ConcurrentDictionary<string, ModuleClient> clients = new ConcurrentDictionary<string, ModuleClient>();

        // Private constructor to ensure single instance
        ModuleClientCache()
        {
        }

        public static ModuleClientCache Instance { get; } = new ModuleClientCache();

        public ModuleClient GetOrCreate(TransportType transportType)
        {
            return this.clients.GetOrAdd(
                transportType.ToString(),
                client => this.CreateModuleClient(transportType));
        }

        ModuleClient CreateModuleClient(TransportType transportType)
        {
            var mqttSetting = new MqttTransportSettings(transportType);

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
            ModuleClient moduleClient = ModuleClient.CreateFromEnvironment(settings);

            moduleClient.ProductInfo = "Microsoft.Azure.Devices.Edge.Functions.Binding";
            return moduleClient;
        }
    }
}
