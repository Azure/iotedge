// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Service
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Service.Modules;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using ILogger = Microsoft.Extensions.Logging.ILogger;

    public class Program
    {
        const string ConfigFileName = "appsettings_agent.json";

        public static int Main()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile(ConfigFileName)
                .AddEnvironmentVariables()
                .Build();

            return MainAsync(configuration).Result;
        }

        static string GetEdgeHubConnectionString(IConfiguration configuration, ILogger logger)
        {
            string connectionString = configuration.GetValue<string>("MMAConnectionString");
            string edgeHubIpInterfaceName = configuration.GetValue<string>("IPInterfaceName");

            // find the local IP address on network interface edgeHubIPInterfaceName
            IPAddress address = NetworkInterface
                .GetAllNetworkInterfaces()
                .FirstOrDefault(inf => inf.Name.Equals(edgeHubIpInterfaceName, StringComparison.OrdinalIgnoreCase))
                ?.GetIPProperties()
                ?.UnicastAddresses
                // We are only interested in IPv4 addresses at this point.
                ?.FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address;
            if (address != null)
            {
                connectionString = $"{connectionString};GatewayHostName={address.ToString()}";
            }
            else
            {
                logger.LogWarning($"Unable to retrieve IP address for network interface {edgeHubIpInterfaceName}");
            }

            return connectionString;
        }

        public static async Task<int> MainAsync(IConfiguration configuration)
        {
            string dockerUriConfig = configuration.GetValue<string>("DockerUri");
            string configSourceConfig = configuration.GetValue<string>("ConfigSource");
            string dockerLoggingDriver = configuration.GetValue<string>("DockerLoggingDriver");
            string backupConfigFilePath = configuration.GetValue<string>("BackupConfigFilePath");

            // build the logger instance for the Program type
            var loggerBuilder = new ContainerBuilder();
            loggerBuilder.RegisterModule(new LoggingModule(Preconditions.CheckNonWhiteSpace(dockerLoggingDriver, nameof(dockerLoggingDriver))));
            var loggerFactory = loggerBuilder.Build().Resolve<ILoggerFactory>();
            ILogger logger = loggerFactory.CreateLogger<Program>();

            string connectionString = GetEdgeHubConnectionString(configuration, logger);
            configuration["EdgeHubConnectionString"] = connectionString;

            var dockerUri = new Uri(dockerUriConfig);
            var builder = new ContainerBuilder();

            switch (configSourceConfig.ToLower())
            {
                case "iothubconnected":
                    builder.RegisterModule(new IotHubConnectedModule(dockerUri, dockerLoggingDriver, connectionString, backupConfigFilePath, configuration));
                    break;
                case "standalone":
                    builder.RegisterModule(new StandaloneModule(dockerUri, dockerLoggingDriver, "config.json", configuration));
                    break;
                default:
                    throw new Exception("ConfigSource not Supported.");
            }

            logger.LogInformation("Starting module management agent.");
            var cts = new CancellationTokenSource();

            void OnUnload(AssemblyLoadContext ctx) => CancelProgram(cts, logger);

            try
            {
                IContainer container = builder.Build();

                AssemblyLoadContext.Default.Unloading += OnUnload;
                Console.CancelKeyPress += (sender, cpe) => { CancelProgram(cts, logger); };

                using (IConfigSource configSource = await container.Resolve<Task<IConfigSource>>())
                {
                    Agent agent = await container.Resolve<Task<Agent>>();

                    // Do another reconcile whenever the config source reports that the desired
                    // configuration has changed.
                    configSource.ModuleSetChanged += async (sender, diff) =>
                    {
                        logger.LogInformation("Applying config change...");
                        await agent.ApplyDiffAsync(diff, CancellationToken.None);
                    };

                    configSource.ModuleSetFailed += (sender, ex) =>
                    {
                        logger.LogError(AgentEventIds.Agent, ex, "Configuration source failure");
                    };

                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await agent.ReconcileAsync(cts.Token);
                        }
                        catch (Exception ex) when (!ex.IsFatal())
                        {
                            logger.LogWarning(AgentEventIds.Agent, ex, "Agent reconcile failed.");
                        }
                        await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
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
