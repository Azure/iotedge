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

            var bindings = new List<PortBinding> { new PortBinding("8080", "80", PortBindingType.Tcp) };
            var module = new DockerModule("webserver", "1.0", ModuleStatus.Running, new DockerConfig("nginx", "latest", bindings));
            var moduleSet = new ModuleSet(new List<IModule> { module });

            DockerClient client = new DockerClientConfiguration(new Uri("http://localhost:2375")).CreateClient();
            var dockerCommandFactory = new DockerCommandFactory(client);
            var commandFactory = new LoggingCommandFactory(dockerCommandFactory, loggerFactory);
            var environment = new DockerEnvironment(client);
            var agent = new Agent(moduleSet, environment, new RestartPlanner(commandFactory));

            for (int i = 0; i < 1000; i++)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                logger.LogInformation("Reconciling... [{i}]", i);
                await agent.ReconcileAsync(CancellationToken.None);
            }
            return 0;
        }
    }
}
