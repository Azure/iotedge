// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    class ModuleClientCache
    {
        const int RetryCount = 5;
        static readonly ITransientErrorDetectionStrategy TimeoutErrorDetectionStrategy = new DelegateErrorDetectionStrategy(ex => ex.HasTimeoutException());

        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(RetryCount, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(4));

        readonly SemaphoreSlim asyncLock = new SemaphoreSlim(1, 1);
        IotHubModuleClient client;

        // Private constructor to ensure single instance
        ModuleClientCache()
        {
        }

        public static ModuleClientCache Instance { get; } = new ModuleClientCache();

        public async Task<IotHubModuleClient> GetOrCreateAsync()
        {
            await this.asyncLock.WaitAsync();
            try
            {
                if (this.client == null)
                {
                    var retryPolicy = new RetryPolicy(TimeoutErrorDetectionStrategy, TransientRetryStrategy);
                    retryPolicy.Retrying += (_, args) =>
                    {
                        Console.WriteLine($"Creating IotHubModuleClient failed with exception {args.LastException}");
                        if (args.CurrentRetryCount < RetryCount)
                        {
                            Console.WriteLine("Retrying...");
                        }
                    };
                    this.client = await retryPolicy.ExecuteAsync(() => this.CreateModuleClient());
                }

                return this.client;
            }
            finally
            {
                this.asyncLock.Release();
            }
        }

        async Task<IotHubModuleClient> CreateModuleClient()
        {
            var options = new IotHubClientOptions(new IotHubClientAmqpSettings(IotHubClientTransportProtocol.Tcp))
            {
                AdditionalUserAgentInfo = "Microsoft.Azure.WebJobs.Extensions.EdgeHub"
            };
            IotHubModuleClient moduleClient = await IotHubModuleClient.CreateFromEnvironmentAsync(options);

            return moduleClient;
        }
    }
}
