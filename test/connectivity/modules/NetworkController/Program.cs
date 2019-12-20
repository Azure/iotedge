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
                Log.LogInformation($"Schedule task with NetworkStatus {controller.NetworkStatus} to start after {delay} Offline frequency {item.OfflineFrequency} Online frequency {item.OnlineFrequency} Run times {item.RunsCount}");

                var taskExecutor = new CountedTaskExecutor(
                    async cs =>
                    {
                        await SetNetworkStatus(controller, true, reporter, cs);
                        await Task.Delay(item.OfflineFrequency, cs);
                        await SetNetworkStatus(controller, false, reporter, cs);
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

        static async Task SetNetworkStatus(INetworkController controller, bool enabled, INetworkStatusReporter reporter, CancellationToken cs)
        {
            await reporter.ReportNetworkStatus(NetworkControllerOperation.SettingRule, enabled, controller.NetworkStatus);
            bool success = await controller.SetEnabledAsync(enabled, cs);
            success = await CheckSetEnabledAsyncResult(success, false, controller, cs);
            await reporter.ReportNetworkStatus(NetworkControllerOperation.RuleSet, enabled, controller.NetworkStatus, success);
        }

        static async Task RemoveAllControllingRules(IList<INetworkController> controllerList, CancellationToken cancellationToken)
        {
            var reporter = new NetworkStatusReporter(Settings.Current.TestResultCoordinatorEndpoint, Settings.Current.ModuleId, Settings.Current.TrackingId);

            foreach (var controller in controllerList)
            {
                bool enabled = await controller.GetEnabledAsync(cancellationToken);
                if (enabled)
                {
                    Log.LogInformation($"Network restriction is enabled with {controller.NetworkStatus}. Setting default");
                    bool online = await controller.SetEnabledAsync(false, cancellationToken);
                    online = await CheckSetEnabledAsyncResult(online, false, controller, cancellationToken);
                    if (!online)
                    {
                        Log.LogError($"Failed to ensure it starts with default values.");
                        throw new TestInitializationException();
                    }
                    else
                    {
                        Log.LogInformation($"Network is online for {controller.NetworkStatus}");
                        await reporter.ReportNetworkStatus(NetworkControllerOperation.RuleSet, false, controller.NetworkStatus, true);
                    }
                }
            }
        }

        static async Task<bool> CheckSetEnabledAsyncResult(bool success, bool enabled, INetworkController controller, CancellationToken cs)
        {
            bool reportedEnabled = await controller.GetEnabledAsync(cs);

            string resultMessage = success ? "succeded" : "failed";
            string networkStatus = reportedEnabled ? "restricted" : "online";
            Log.LogInformation($"Command SetEnabledAsync to {enabled} execution {resultMessage}, network status {networkStatus}");

            return success && reportedEnabled == enabled;
        }
    }
}
