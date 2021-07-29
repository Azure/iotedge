// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
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

            var controllers = new List<INetworkController>();

            try
            {
                var networkInterfaceName = DockerHelper.GetDockerInterfaceName();

                if (networkInterfaceName.HasValue)
                {
                    await networkInterfaceName.ForEachAsync(async name =>
                    {
                        string hubHostname = GetHostnameForExternalTraffic();
                        var offline = new OfflineController(name, hubHostname, Settings.Current.NetworkRunProfile.ProfileSetting);
                        var satellite = new SatelliteController(name, hubHostname, Settings.Current.NetworkRunProfile.ProfileSetting);
                        var cellular = new CellularController(name, hubHostname, Settings.Current.NetworkRunProfile.ProfileSetting);
                        controllers.AddRange(new List<INetworkController> { offline, satellite, cellular });

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
                                await SetToggleConnectivityMethod(name, cts.Token);
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
            await CleanupControllingRulesOnShutdown(controllers, CancellationToken.None);
            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
        }

        private static ITransportSettings[] GetTransportSettings(TransportType transportType)
        {
            switch (transportType)
            {
                case TransportType.Mqtt:
                case TransportType.Mqtt_Tcp_Only:
                    MqttTransportSettings settings_mqtt = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
                    Settings.Current.Proxy.ForEach(p => settings_mqtt.Proxy = p);
                    return new ITransportSettings[] { settings_mqtt };
                case TransportType.Mqtt_WebSocket_Only:
                    MqttTransportSettings settings_mqttws = new MqttTransportSettings(TransportType.Mqtt_WebSocket_Only);
                    Settings.Current.Proxy.ForEach(p => settings_mqttws.Proxy = p);
                    return new ITransportSettings[] { settings_mqttws };
                case TransportType.Amqp_WebSocket_Only:
                    AmqpTransportSettings settings_amqpws = new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only);
                    Settings.Current.Proxy.ForEach(p => settings_amqpws.Proxy = p);
                    return new ITransportSettings[] { settings_amqpws };
                default:
                    AmqpTransportSettings settings_amqp = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
                    Settings.Current.Proxy.ForEach(p => settings_amqp.Proxy = p);
                    return new ITransportSettings[] { settings_amqp };
            }
        }

        private static async Task SetToggleConnectivityMethod(string networkInterfaceName, CancellationToken token)
        {
            // Setting GatewayHostname to empty, since the module will be talking directly to IoTHub, bypassing edge
            // NetworkController is on the host, so it should always have connection
            Environment.SetEnvironmentVariable("IOTEDGE_GATEWAYHOSTNAME", string.Empty);
            ITransportSettings[] settings = GetTransportSettings(Settings.Current.TransportType);
            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await moduleClient.SetMethodHandlerAsync("toggleConnectivity", ToggleConnectivity, new Tuple<string, CancellationToken>(networkInterfaceName, token));
        }

        private static async Task<MethodResponse> ToggleConnectivity(MethodRequest methodRequest, object userContext)
        {
            Log.LogInformation("Direct method toggleConnectivity has been invoked.");
            (string networkInterfaceName, CancellationToken token) = (Tuple<string, CancellationToken>)userContext;

            // true for network on (restriction disabled), false for network off (restriction enabled)
            if (!bool.TryParse(JObject.Parse(methodRequest.DataAsJson)["networkOnValue"].ToString(), out bool networkOnValue))
            {
                throw new ArgumentException($"Unable to parse methodRequest. JsonData: {methodRequest.DataAsJson}");
            }

            Log.LogInformation($"Toggling network {networkInterfaceName} {(networkOnValue ? "on" : "off")}");
            INetworkStatusReporter reporter = new NetworkStatusReporter(Settings.Current.TestResultCoordinatorEndpoint, Settings.Current.ModuleId, Settings.Current.TrackingId);
            NetworkProfileSetting customNetworkProfileSetting = Settings.Current.NetworkRunProfile.ProfileSetting;
            customNetworkProfileSetting.PackageLoss = 100;
            string hubHostname = GetHostnameForExternalTraffic();
            var controller = new OfflineController(networkInterfaceName, hubHostname, customNetworkProfileSetting);
            NetworkControllerStatus networkControllerStatus = networkOnValue ? NetworkControllerStatus.Disabled : NetworkControllerStatus.Enabled;
            await SetNetworkControllerStatus(controller, networkControllerStatus, reporter, token);
            return new MethodResponse((int)HttpStatusCode.OK);
        }

        private static string GetHostnameForExternalTraffic()
        {
            // TODO: clean up option syntax
            string externalTrafficHostname = string.Empty;
            if (Settings.Current.Proxy.HasValue)
            {
                externalTrafficHostname = Settings.Current.Proxy.Expect(() => new Exception("Proxy should have value")).ToString();
            }
            else if (!string.IsNullOrEmpty(Settings.Current.ParentHostname))
            {
                externalTrafficHostname = Settings.Current.ParentHostname;
            }
            else
            {
                externalTrafficHostname = Settings.Current.IotHubHostname;
            }

            Log.LogInformation("External traffic ip: {0}", externalTrafficHostname);
            return externalTrafficHostname;
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

        static async Task CleanupControllingRulesOnShutdown(IList<INetworkController> controllerList, CancellationToken cancellationToken)
        {
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
                        Log.LogError($"Failed to ensure that NetworkController shuts down with default values.");
                        throw new TestShutdownException();
                    }
                }
            }

            Log.LogInformation($"Network is online");
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
