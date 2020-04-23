// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    class Program
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<Program>();

        static async Task Main()
        {
            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Log);

            Log.LogInformation($"Starting with {Settings.Current.NetworkRunProfile.ProfileType} Settings: {Settings.Current.NetworkRunProfile.ProfileSetting}");

            try
            {
                var networkInterfaceName = DockerHelper.GetDockerInterfaceName();

                if (networkInterfaceName.HasValue)
                {
                    await networkInterfaceName.ForEachAsync(async name =>
                    {
                        var offline = new OfflineController(name, Settings.Current.IotHubHostname, Settings.Current.NetworkRunProfile.ProfileSetting);
                        var satellite = new SatelliteController(name, Settings.Current.IotHubHostname, Settings.Current.NetworkRunProfile.ProfileSetting);
                        var cellular = new CellularController(name, Settings.Current.IotHubHostname, Settings.Current.NetworkRunProfile.ProfileSetting);
                        var controllers = new List<INetworkController>() { offline, satellite, cellular };

                        // Reset network status before start delay to ensure network status is in designed state before test starts.
                        var sw = new Stopwatch();
                        sw.Start();
                        await RemoveAllControllingRules(controllers, cts.Token);
                        sw.Stop();
                        TimeSpan durationBeforeTestStart = Settings.Current.StartAfter <= sw.Elapsed ? TimeSpan.Zero : Settings.Current.StartAfter - sw.Elapsed;

                        Log.LogInformation($"Delay {durationBeforeTestStart} before starting network controller.");
                        await Task.Delay(durationBeforeTestStart, cts.Token);

                        switch (Settings.Current.NetworkRunProfile.ProfileType)
                        {
                            case NetworkControllerType.Offline:
                                await StartAsync(offline, cts.Token);
                                break;
                            case NetworkControllerType.Satellite:
                                await StartAsync(satellite, cts.Token);
                                break;
                            case NetworkControllerType.Cellular:
                                await StartAsync(cellular, cts.Token);
                                break;
                            case NetworkControllerType.Online:
                                ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(TransportType.Amqp_Tcp_Only);
                                await moduleClient.SetMethodHandlerAsync("toggleConnectivity", ToggleConnectivity, new Tuple<string, CancellationToken>(name, cts.Token));
                                Log.LogInformation($"No restrictions to be set, running as online");
                                break;
                            default:
                                throw new NotSupportedException($"Network type {Settings.Current.NetworkRunProfile.ProfileType} is not supported.");
                        }
                    });
                }
                else
                {
                    Log.LogError($"No network interface found for docker network {Settings.Current.NetworkId}");
                }
            }
            catch (Exception ex)
            {
                Log.LogError(ex, $"Unexpected exception thrown from {nameof(Main)} method");
            }

            await cts.Token.WhenCanceled();
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
        }

        private static async Task<MethodResponse> ToggleConnectivity(MethodRequest methodRequest, object userContext)
        {
            var tup = (Tuple<string, CancellationToken>)userContext;

            // true for network on (restriction disabled), false for network off (restriction enabled)
            bool networkOnValue = (bool)JObject.Parse(methodRequest.DataAsJson)["networkOnValue"];
            INetworkStatusReporter reporter = new NetworkStatusReporter(Settings.Current.TestResultCoordinatorEndpoint, Settings.Current.ModuleId, Settings.Current.TrackingId);
            var controller = new OfflineController(tup.Item1, Settings.Current.IotHubHostname, Settings.Current.NetworkRunProfile.ProfileSetting);
            NetworkControllerStatus ncs = networkOnValue ? NetworkControllerStatus.Disabled : NetworkControllerStatus.Enabled;
            await SetNetworkControllerStatus(controller, ncs, reporter, tup.Item2);
            return new MethodResponse((int)HttpStatusCode.OK);
        }

        static async Task StartAsync(INetworkController controller, CancellationToken cancellationToken)
        {
            INetworkStatusReporter reporter = new NetworkStatusReporter(Settings.Current.TestResultCoordinatorEndpoint, Settings.Current.ModuleId, Settings.Current.TrackingId);
            foreach (Frequency item in Settings.Current.Frequencies)
            {
                Log.LogInformation($"Schedule task for type {controller.NetworkControllerType} with enable network control frequency {item.OfflineFrequency}, disable network control frequency {item.OnlineFrequency}, and run times {item.RunsCount}.");

                var taskExecutor = new CountedTaskExecutor(
                    async cs =>
                    {
                        await SetNetworkControllerStatus(controller, NetworkControllerStatus.Enabled, reporter, cs);
                        await Task.Delay(item.OfflineFrequency, cs);
                        await SetNetworkControllerStatus(controller, NetworkControllerStatus.Disabled, reporter, cs);
                    },
                    TimeSpan.Zero,
                    item.OnlineFrequency,
                    item.RunsCount,
                    Log,
                    "restrict/default");

                await taskExecutor.Schedule(cancellationToken);
            }
        }

        static async Task SetNetworkControllerStatus(INetworkController controller, NetworkControllerStatus networkControllerStatus, INetworkStatusReporter reporter, CancellationToken cs)
        {
            await reporter.ReportNetworkStatusAsync(NetworkControllerOperation.SettingRule, networkControllerStatus, controller.NetworkControllerType);
            bool success = await controller.SetNetworkControllerStatusAsync(networkControllerStatus, cs);
            success = await CheckSetNetworkControllerStatusAsyncResult(success, networkControllerStatus, controller, cs);
            await reporter.ReportNetworkStatusAsync(NetworkControllerOperation.RuleSet, networkControllerStatus, controller.NetworkControllerType, success);
        }

        static async Task RemoveAllControllingRules(IList<INetworkController> controllerList, CancellationToken cancellationToken)
        {
            var reporter = new NetworkStatusReporter(Settings.Current.TestResultCoordinatorEndpoint, Settings.Current.ModuleId, Settings.Current.TrackingId);
            await reporter.ReportNetworkStatusAsync(NetworkControllerOperation.SettingRule, NetworkControllerStatus.Disabled, NetworkControllerType.All);

            foreach (var controller in controllerList)
            {
                NetworkControllerStatus networkControllerStatus = await controller.GetNetworkControllerStatusAsync(cancellationToken);
                if (networkControllerStatus != NetworkControllerStatus.Disabled)
                {
                    Log.LogInformation($"Network restriction is enabled for {controller.NetworkControllerType}. Setting default");
                    bool online = await controller.SetNetworkControllerStatusAsync(NetworkControllerStatus.Disabled, cancellationToken);
                    online = await CheckSetNetworkControllerStatusAsyncResult(online, NetworkControllerStatus.Disabled, controller, cancellationToken);
                    if (!online)
                    {
                        Log.LogError($"Failed to ensure it starts with default values.");
                        await reporter.ReportNetworkStatusAsync(NetworkControllerOperation.RuleSet, NetworkControllerStatus.Enabled, controller.NetworkControllerType, true);
                        throw new TestInitializationException();
                    }
                }
            }

            Log.LogInformation($"Network is online");
            await reporter.ReportNetworkStatusAsync(NetworkControllerOperation.RuleSet, NetworkControllerStatus.Disabled, NetworkControllerType.All, true);
        }

        static async Task<bool> CheckSetNetworkControllerStatusAsyncResult(bool success, NetworkControllerStatus networkControllerStatus, INetworkController controller, CancellationToken cs)
        {
            NetworkControllerStatus reportedStatus = await controller.GetNetworkControllerStatusAsync(cs);

            string resultMessage = success ? "succeded" : "failed";
            Log.LogInformation($"Command SetNetworkControllerStatus to {networkControllerStatus} execution {resultMessage}, network status {reportedStatus}");

            return success && reportedStatus == networkControllerStatus;
        }
    }
}
