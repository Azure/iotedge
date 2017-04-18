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

    class Program
    {
        public static int Main(string[] args) => MainAsync(args).Result;

        static async Task<int> MainAsync(string[] args)
        {
            ILoggerFactory factory = new LoggerFactory()
                .AddConsole();
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