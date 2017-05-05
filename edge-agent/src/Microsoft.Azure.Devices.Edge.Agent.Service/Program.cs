// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;
    using ILogger = Microsoft.Extensions.Logging.ILogger;
    using Microsoft.Azure.Devices.Edge.Agent.Core.ConfigSources;

    class Program
    {
        public static int Main(string[] args) => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Logger loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] - {Message}{NewLine}{Exception}"
                )
                .CreateLogger();

            ILoggerFactory loggerFactory = new LoggerFactory()
                .AddSerilog(loggerConfig);
            ILogger logger = loggerFactory.CreateLogger<Program>();

            logger.LogInformation("Starting module management agent.");

            DockerClient client = new DockerClientConfiguration(new Uri("http://localhost:2375")).CreateClient();
            var dockerCommandFactory = new DockerCommandFactory(client);
            var commandFactory = new LoggingCommandFactory(dockerCommandFactory, loggerFactory);
            var environment = new DockerEnvironment(client);

            // We only support Docker modules at this point.
            var moduleSetSerde = new ModuleSetSerde(
                new Dictionary<string, Type>
                {
                    { "docker", typeof(DockerModule) }
                }
            );

            using (FileConfigSource configSource = await FileConfigSource.Create("config.json", moduleSetSerde))
            {
                ModuleSet moduleSet = await configSource.GetConfigAsync();
                var agent = new Agent(moduleSet, environment, new RestartPlanner(commandFactory));

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
    }
}
