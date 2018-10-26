// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Agent.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class Program
    {
        static readonly TimeSpan ShutdownWaitPeriod = TimeSpan.FromMinutes(1);
        const string ConfigFileName = "appsettings_agent.json";

        public static int Main()
        {
            Console.WriteLine($"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt")}] Edge Agent Main()");
            try
            {
                var appSettings = new AgentAppSettings(ConfigFileName);
                return MainAsync(appSettings).Result;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        public static async Task<int> MainAsync(IAgentAppSettings appSettings)
        {
            // Bring up the logger before anything else so we can log errors ASAP
            ILogger logger = SetupLogger(appSettings);

            logger.LogInformation("Starting module management agent.");

            if (appSettings.VersionInfo != VersionInfo.Empty)
            {
                logger.LogInformation($"Version - {appSettings.VersionInfo.ToString(true)}");
            }

            LogLogo(logger);

            IEnumerable<AuthConfig> dockerAuthConfig;

            try
            {
                dockerAuthConfig = appSettings.DockerRegistryAuthConfigSection.Get<List<AuthConfig>>() ?? new List<AuthConfig>();
            }
            catch (Exception ex)
            {
                logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error reading the Agent's appSettings.");
                return 1;
            }

            IContainer container;
            try
            {
                var builder = new ContainerBuilder();
                builder.RegisterModule(new LoggingModule(appSettings.DockerLoggingDriver, appSettings.DockerLoggingOptions));
                Option<string> productInfo = appSettings.VersionInfo != VersionInfo.Empty ? Option.Some(appSettings.VersionInfo.ToString()) : Option.None<string>();

                switch (appSettings.RuntimeMode)
                {
                    case EdgeRuntimeMode.Docker:
                        var dockerUri = new Uri(appSettings.DockerUri);
                        builder.RegisterModule(new AgentModule(appSettings.MaxRestartCount, appSettings.IntensiveCareTime, appSettings.CoolOffTimeUnit, appSettings.UsePersistentStorage, appSettings.StoragePath));
                        builder.RegisterModule(new DockerModule(appSettings.DeviceConnectionString, appSettings.EdgeDeviceHostName, dockerUri, dockerAuthConfig, appSettings.UpstreamProtocol, productInfo));
                        break;

                    case EdgeRuntimeMode.Iotedged:
                        builder.RegisterModule(new AgentModule(appSettings.MaxRestartCount, appSettings.IntensiveCareTime, appSettings.CoolOffTimeUnit, appSettings.UsePersistentStorage, appSettings.StoragePath, Option.Some(new Uri(appSettings.WorkloadUri)), appSettings.ModuleId, Option.Some(appSettings.ModuleGenerationId)));
                        builder.RegisterModule(new EdgeletModule(appSettings.IoTHubHostName, appSettings.EdgeDeviceHostName, appSettings.DeviceId, new Uri(appSettings.ManagementUri), new Uri(appSettings.WorkloadUri), dockerAuthConfig, appSettings.UpstreamProtocol, productInfo));
                        break;

                    default:
                        throw new InvalidOperationException($"Runtime mode '{appSettings.RuntimeMode}' not supported.");
                }

                switch (appSettings.ConfigSource)
                {
                    case ConfigSource.Twin:
                        builder.RegisterModule(new TwinConfigSourceModule(appSettings));
                        break;

                    case ConfigSource.Local:
                        builder.RegisterModule(new FileConfigSourceModule("config.json", appSettings));
                        break;

                    default:
                        throw new InvalidOperationException($"ConfigSource '{appSettings.ConfigSource}' not supported.");
                }

                container = builder.Build();
            }
            catch (Exception ex)
            {
                logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error building application.");
                return 1;
            }

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler)
                = ShutdownHandler.Init(ShutdownWaitPeriod, logger);

            int returnCode;
            using (IConfigSource unused = await container.Resolve<Task<IConfigSource>>())
            {
                Option<Agent> agentOption = Option.None<Agent>();

                try
                {
                    Agent agent = await container.Resolve<Task<Agent>>();
                    agentOption = Option.Some(agent);
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await agent.ReconcileAsync(cts.Token);
                        }
                        catch (Exception ex) when (!ex.IsFatal())
                        {
                            logger.LogWarning(AgentEventIds.Agent, ex, "Agent reconcile concluded with errors.");
                        }

                        await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
                    }

                    logger.LogInformation("Closing module management agent.");

                    returnCode = 0;
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Main thread terminated");
                    returnCode = 0;
                }
                catch (Exception ex)
                {
                    logger.LogCritical(AgentEventIds.Agent, ex, "Fatal error starting Agent.");
                    returnCode = 1;
                }

                // Attempt to report shutdown of Agent
                await Cleanup(agentOption, logger);
                completed.Set();
            }

            handler.ForEach(h => GC.KeepAlive(h));
            return returnCode;
        }

        static ILogger SetupLogger(IAgentAppSettings appSettings)
        {
            Logger.SetLogLevel(appSettings.RuntimeLogLevel);
            return Logger.Factory.CreateLogger<Program>();
        }

        static Task Cleanup(Option<Agent> agentOption, ILogger logger)
        {
            var closeCts = new CancellationTokenSource(ShutdownWaitPeriod);

            try
            {
                return agentOption.ForEachAsync(a => a.HandleShutdown(closeCts.Token));
            }
            catch (Exception ex)
            {
                logger.LogError(AgentEventIds.Agent, ex, "Error on shutdown");
                return Task.CompletedTask;
            }
        }

        static void LogLogo(ILogger logger)
        {
            logger.LogInformation(
                @"
        █████╗ ███████╗██╗   ██╗██████╗ ███████╗
       ██╔══██╗╚══███╔╝██║   ██║██╔══██╗██╔════╝
       ███████║  ███╔╝ ██║   ██║██████╔╝█████╗
       ██╔══██║ ███╔╝  ██║   ██║██╔══██╗██╔══╝
       ██║  ██║███████╗╚██████╔╝██║  ██║███████╗
       ╚═╝  ╚═╝╚══════╝ ╚═════╝ ╚═╝  ╚═╝╚══════╝

 ██╗ ██████╗ ████████╗    ███████╗██████╗  ██████╗ ███████╗
 ██║██╔═══██╗╚══██╔══╝    ██╔════╝██╔══██╗██╔════╝ ██╔════╝
 ██║██║   ██║   ██║       █████╗  ██║  ██║██║  ███╗█████╗
 ██║██║   ██║   ██║       ██╔══╝  ██║  ██║██║   ██║██╔══╝
 ██║╚██████╔╝   ██║       ███████╗██████╔╝╚██████╔╝███████╗
 ╚═╝ ╚═════╝    ╚═╝       ╚══════╝╚═════╝  ╚═════╝ ╚══════╝
");
        }
    }
}
