// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    class ModuleClientCache
    {
        const int RetryCount = 5;
        static readonly ITransientErrorDetectionStrategy TimeoutErrorDetectionStrategy = new DelegateErrorDetectionStrategy(ex => ex.HasTimeoutException());

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(RetryCount, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));

        readonly AsyncLock asyncLock = new AsyncLock();
        ModuleClient client;

        // Private constructor to ensure single instance
        ModuleClientCache()
        {
        }

        public static ModuleClientCache Instance { get; } = new ModuleClientCache();

        public async Task<ModuleClient> GetOrCreateAsync()
        {
            using (await this.asyncLock.LockAsync())
            {
                if (this.client == null)
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
                    this.client = await retryPolicy.ExecuteAsync(() => this.CreateModuleClient());
                }

                return this.client;
            }
        }

        async Task<ModuleClient> CreateModuleClient()
        {
            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(TransportType.Mqtt_Tcp_Only);

            moduleClient.ProductInfo = "Microsoft.Azure.WebJobs.Extensions.EdgeHub";
            return moduleClient;
        }
    }
}
