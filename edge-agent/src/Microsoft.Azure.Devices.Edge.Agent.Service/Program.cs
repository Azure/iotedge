// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Extensions.Logging;
    using Microsoft.Azure.Devices.Edge.Agent.Service.Modules;
    using Microsoft.Extensions.Configuration;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    class Program
    {
        const string ConfigFileName = "appsettings.json";

        public static int Main(string[] args) => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile(ConfigFileName)
                .AddEnvironmentVariables()
                .Build();

            string connectionString = configuration.GetValue<string>("MMAConnectionString");
            string dockerUriConfig = configuration.GetValue<string>("DockerUri");
            string configSourceConfig = configuration.GetValue<string>("ConfigSource");

            var dockerUri = new Uri(dockerUriConfig);

            var builder = new ContainerBuilder();

            switch(configSourceConfig.ToLower())
            {
                case "iothubconnected":
                    builder.RegisterModule(new IotHubConnectedModule(dockerUri, connectionString));
                    break;
                case "standalone":
                    builder.RegisterModule(new StandaloneModule(dockerUri, "config.json"));
                    break;
                default:
                    throw new Exception("ConfigSource not Supported.");
            }

            IContainer container = builder.Build();
            var loggerFactory = container.Resolve<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger<Program>();
            
            logger.LogInformation("Starting module management agent.");

            try
            {

                using (IConfigSource configSource = await container.Resolve<Task<IConfigSource>>())
                {
                    Agent agent = await container.Resolve<Task<Agent>>();

                    // Do another reconcile whenever the config source reports that the desired
                    // configuration has changed.
                    configSource.Changed += async (sender, diff) =>
                    {
                        logger.LogInformation("Applying config change...");
                        await agent.ApplyDiffAsync(diff, CancellationToken.None);
                    };

                    for (int i = 0; i < 1000; i++)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                        logger.LogInformation($"Reconciling [scheduled]... [{i}]");
                        await agent.ReconcileAsync(CancellationToken.None);
                    }
                }
                return 0;
            }
            catch (Exception e)
            {
                logger.LogError($"ERROR: {e}");
                return 1;
            }
        }
    }
}
