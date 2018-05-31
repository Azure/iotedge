// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

    class ModuleClientCache
    {
        readonly ConcurrentDictionary<string, Task<ModuleClient>> clients = new ConcurrentDictionary<string, Task<ModuleClient>>();

        // Private constructor to ensure single instance
        ModuleClientCache()
        {
        }

        public static ModuleClientCache Instance { get; } = new ModuleClientCache();

        public Task<ModuleClient> GetOrCreateAsync(TransportType transportType)
        {
            return this.clients.GetOrAdd(
                transportType.ToString(),
                client => this.CreateModuleClient(transportType));
        }

        async Task<ModuleClient> CreateModuleClient(TransportType transportType)
        {
            var mqttSetting = new MqttTransportSettings(transportType);

            ITransportSettings[] settings = { mqttSetting };
            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings).ConfigureAwait(false);

            moduleClient.ProductInfo = "Microsoft.Azure.Devices.Edge.Functions.Binding";
            return moduleClient;
        }
    }
}
