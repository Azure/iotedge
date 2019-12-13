// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<Program>();

        static async Task Main()
        {
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Log);

            Log.LogInformation($"Starting with {Settings.Current.NetworkControllerMode}");

            var networkInterfaceName = DockerHelper.GetDockerInterfaceName();
            if (networkInterfaceName.HasValue)
            {
                await networkInterfaceName.ForEachAsync(
                    async name =>
                    {
                        var nic = new NetworkInterfaceOfflineController(name);
                        var firewall = new FirewallOfflineController(name);
                        var satellite = new SatelliteController(name);
                        var controllers = new List<IController>() { nic, firewall, satellite };
                        await RemoveAllControllingRules(controllers, cts.Token);

                        switch (Settings.Current.NetworkControllerMode)
                        {
                            case NetworkControllerMode.OfflineNetworkInterface:
                                await StartAsync(nic, cts.Token);
                                break;
                            case NetworkControllerMode.OfflineTrafficController:
                                await StartAsync(firewall, cts.Token);
                                break;
                            case NetworkControllerMode.SatelliteTrafficController:
                                await StartAsync(satellite, cts.Token);
                                break;
                        }
                    });

                await cts.Token.WhenCanceled();
                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
            }
            else
            {
                Log.LogError($"No network interface found for docker network {Settings.Current.NetworkId}");
            }
        }

        static async Task StartAsync(IController controller, CancellationToken cancellationToken)
        {
            var delay = Settings.Current.StartAfter;

            INetworkStatusReporter reporter = new NetworkReporter();
            foreach (Frequency item in Settings.Current.Frequencies)
            {
                Log.LogInformation($"Schedule task with {controller.Description} to start after {delay} Offline frequency {item.OfflineFrequency} Online frequency {item.OnlineFrequency} Run times {item.RunsCount}");

                var taskExecutor = new CountedTaskExecutor(
                    async cs =>
                    {
                        await SetNetworkStatus(controller, NetworkStatus.Restricted, reporter, cs);
                        await Task.Delay(item.OfflineFrequency, cs);
                        await SetNetworkStatus(controller, NetworkStatus.Default, reporter, cs);
                    },
                    delay,
                    item.OfflineFrequency,
                    item.RunsCount,
                    Log,
                    "restrict/default");

                await taskExecutor.Schedule(cancellationToken);

                // Only needs to set the start delay for first frequency, after that reset to 0
                delay = TimeSpan.FromSeconds(0);
            }
        }

        static async Task SetNetworkStatus(IController controller, NetworkStatus status, INetworkStatusReporter reporter, CancellationToken cs)
        {
            await reporter.ReportNetworkStatus(NetworkControllerOperation.SettingRule, status, controller.Description);
            bool success = await controller.SetStatus(status, cs);
            await reporter.ReportNetworkStatus(NetworkControllerOperation.RuleSet, status, controller.Description, success);
        }

        static async Task RemoveAllControllingRules(IList<IController> controllerList, CancellationToken cancellationToken)
        {
            var reporter = new NetworkReporter();

            foreach (var controller in controllerList)
            {
                NetworkStatus status = await controller.GetStatus(cancellationToken);
                if (status != NetworkStatus.Default)
                {
                    Log.LogInformation($"Network is {status} with {controller.Description}, setting default");
                    bool online = await controller.SetStatus(NetworkStatus.Default, cancellationToken);
                    if (!online)
                    {
                        Log.LogError($"Failed to ensure it starts with default values.");
                        throw new TestInitializationException();
                    }
                }
            }

            Log.LogInformation($"Network is online");
            await reporter.ReportNetworkStatus(NetworkControllerOperation.RuleSet, NetworkStatus.Default, "All", true);
        }
    }
}
