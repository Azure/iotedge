// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
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
            var cts = new CancellationTokenSource();

            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts, logger);

            AssemblyLoadContext.Default.Unloading += OnUnload;
            Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts, logger); };

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

                    configSource.Failed += (sender, ex) =>
                    {
                        logger.LogError(AgentEventIds.Agent, ex, "Configuration source failure");
                    };

                    int i = 1;
                    while (!cts.Token.IsCancellationRequested)
                    {
                        logger.LogInformation($"Reconciling [scheduled]... [{i}]");

                        try
                        {
                            await agent.ReconcileAsync(cts.Token);
                            logger.LogInformation($"Reconciling finished [scheduled]... [{i}]");
                        }
                        catch (Exception ex) when (!ex.IsFatal())
                        {
                            logger.LogWarning(AgentEventIds.Agent, ex, "Agent reconcile failed.");
                        }
                        await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
                        i++;
                    }
                    AssemblyLoadContext.Default.Unloading -= OnUnload;
                    logger.LogInformation("Closing module management agent.");

                }
                return 0;
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Main thread terminated");
                AssemblyLoadContext.Default.Unloading -= OnUnload;
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error starting Agent.");
                return 1;
            }
        }

        static void CancelProgram(CancellationTokenSource cts, ILogger logger)
        {
            logger.LogInformation("Termination requested, closing.");
            cts.Cancel();
        }
    }
}
