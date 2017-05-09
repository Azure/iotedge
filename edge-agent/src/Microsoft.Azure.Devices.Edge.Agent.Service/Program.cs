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

    class Program
    {
        public static int Main(string[] args) => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            // TODO set these through config file or args
            var dockerUri = new Uri("http://localhost:2375");
            string configFile = "config.json";

            var builder = new ContainerBuilder();
            builder.RegisterModule(new StandaloneModule(dockerUri, configFile));
            IContainer container = builder.Build();

            var loggerFactory = container.Resolve<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger<Program>();
            logger.LogInformation("Starting module management agent.");

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
    }
}
