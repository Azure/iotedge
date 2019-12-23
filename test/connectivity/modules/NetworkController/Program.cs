// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkControllerResult;
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
                        var firewall = new FirewallOfflineController(name, Settings.Current.IotHubHostname);
                        var satellite = new SatelliteController(name);
                        var controllers = new List<INetworkController>() { firewall, satellite };
                        await RemoveAllControllingRules(controllers, cts.Token);

                        switch (Settings.Current.NetworkControllerMode)
                        {
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

        static async Task StartAsync(INetworkController controller, CancellationToken cancellationToken)
        {
            var delay = Settings.Current.StartAfter;

            INetworkStatusReporter reporter = new NetworkStatusReporter(Settings.Current.TestResultCoordinatorEndpoint, Settings.Current.ModuleId, Settings.Current.TrackingId);
            foreach (Frequency item in Settings.Current.Frequencies)
            {
                Log.LogInformation($"Schedule task for type {controller.NetworkControllerType} to start after {delay} Offline frequency {item.OfflineFrequency} Online frequency {item.OnlineFrequency} Run times {item.RunsCount}");

                var taskExecutor = new CountedTaskExecutor(
                    async cs =>
                    {
                        await SetNetworkStatus(controller, NetworkStatus.Enabled, reporter, cs);
                        await Task.Delay(item.OfflineFrequency, cs);
                        await SetNetworkStatus(controller, NetworkStatus.Disabled, reporter, cs);
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

        static async Task SetNetworkStatus(INetworkController controller, NetworkStatus networkStatus, INetworkStatusReporter reporter, CancellationToken cs)
        {
            await reporter.ReportNetworkStatus(NetworkControllerOperation.SettingRule, networkStatus, controller.NetworkControllerType);
            bool success = await controller.SetNetworkStatusAsync(networkStatus, cs);
            success = await CheckSetNetworkStatusAsyncResult(success, NetworkStatus.Disabled, controller, cs);
            await reporter.ReportNetworkStatus(NetworkControllerOperation.RuleSet, networkStatus, controller.NetworkControllerType, success);
        }

        static async Task RemoveAllControllingRules(IList<INetworkController> controllerList, CancellationToken cancellationToken)
        {
            var reporter = new NetworkStatusReporter(Settings.Current.TestResultCoordinatorEndpoint, Settings.Current.ModuleId, Settings.Current.TrackingId);

            foreach (var controller in controllerList)
            {
                NetworkStatus networkStatus = await controller.GetNetworkStatusAsync(cancellationToken);
                if (networkStatus != NetworkStatus.Disabled)
                {
                    Log.LogInformation($"Network restriction is enabled for {controller.NetworkControllerType}. Setting default");
                    bool online = await controller.SetNetworkStatusAsync(NetworkStatus.Disabled, cancellationToken);
                    online = await CheckSetNetworkStatusAsyncResult(online, NetworkStatus.Disabled, controller, cancellationToken);
                    if (!online)
                    {
                        Log.LogError($"Failed to ensure it starts with default values.");
                        throw new TestInitializationException();
                    }
                    else
                    {
                        Log.LogInformation($"Network is online for {controller.NetworkControllerType}");
                        await reporter.ReportNetworkStatus(NetworkControllerOperation.RuleSet, NetworkStatus.Disabled, controller.NetworkControllerType, true);
                    }
                }
            }
        }

        static async Task<bool> CheckSetNetworkStatusAsyncResult(bool success, NetworkStatus networkStatus, INetworkController controller, CancellationToken cs)
        {
            NetworkStatus reportedStatus = await controller.GetNetworkStatusAsync(cs);

            string resultMessage = success ? "succeded" : "failed";
            Log.LogInformation($"Command SetEnabledAsync to {networkStatus} execution {resultMessage}, network status {networkStatus}");

            return success && reportedStatus == networkStatus;
        }
    }
}
