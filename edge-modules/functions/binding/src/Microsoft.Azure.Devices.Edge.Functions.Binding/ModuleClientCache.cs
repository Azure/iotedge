// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using ExponentialBackoff = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.ExponentialBackoff;

    class ModuleClientCache
    {
        const int RetryCount = 5;
        static readonly ITransientErrorDetectionStrategy TimeoutErrorDetectionStrategy = new DelegateErrorDetectionStrategy(ex => ex.HasTimeoutException());
        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(RetryCount, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));
        readonly ConcurrentDictionary<string, Task<ModuleClient>> clients = new ConcurrentDictionary<string, Task<ModuleClient>>();

        // Private constructor to ensure single instance
        ModuleClientCache()
        {
        }

        public static ModuleClientCache Instance { get; } = new ModuleClientCache();

        public Task<ModuleClient> GetOrCreateAsync(TransportType transportType) =>
            this.clients.GetOrAdd(
                transportType.ToString(),
                client =>
                {
                    var retryPolicy = new RetryPolicy(TimeoutErrorDetectionStrategy, TransientRetryStrategy);
                    retryPolicy.Retrying += (_, args) =>
                    {
                        Console.WriteLine($"Creating ModuleClient failed with exception {args.LastException}");
                        if (args.CurrentRetryCount < RetryCount)
                        {
                            Console.WriteLine("Retrying...");
                        }
                    };
                    return retryPolicy.ExecuteAsync(() => this.CreateModuleClient(transportType));
                });


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
