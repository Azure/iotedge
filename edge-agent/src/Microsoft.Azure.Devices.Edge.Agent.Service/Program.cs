// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Planners;
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

            ILoggerFactory factory = new LoggerFactory()
                .AddSerilog(loggerConfig);
            ILogger logger = factory.CreateLogger<Program>();

            logger.LogInformation("Starting module management agent.");

            var commandFactory = new LoggingCommandFactory(NullCommandFactory.Instance, factory);
            var agent = new Agent(ModuleSet.Empty, NullEnvironment.Instance, new RestartPlanner(commandFactory));

            for (int i = 0; i < 1000; i++)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));
                logger.LogInformation("Reconciling... [{i}]", i);
                await agent.Reconcile(CancellationToken.None);
            }
            return 0;
        }
    }
}