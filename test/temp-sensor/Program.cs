// Copyright (c) Microsoft. All rights reserved.

namespace temp_sensor
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Threading;
    using System.Threading.Tasks;
    using common;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.Devices.Edge.Util;

    [Command(
        Name = "temp-sensor",
        Description = "tempSensor end-to-end test",
        ExtendedHelpText = @"

The following variables must be set in your environment:
    E2E_IOT_HUB_CONNECTION_STRING
    E2E_EVENT_HUB_ENDPOINT
If you specify `--registry` and `--user`, the following variable must also be set:
    E2E_CONTAINER_REGISTRY_PASSWORD
"
    )]
    [HelpOption]
    [RegistryAndUserOptionsMustBeSpecifiedTogether()]
    class Program
    {
        const string DefaultAgentImage = "mcr.microsoft.com/azureiotedge-agent:1.0";
        const string DefaultHubImage = "mcr.microsoft.com/azureiotedge-hub:1.0";
        const string DefaultSensorImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0";

        [Required]
        [Argument(0, Name = "device-id", Description = "Device ID")]
        public string DeviceId { get; }

        [Required]
        [Argument(1, Name = "installer-path", Description = "Path to IotEdgeSecurityDaemon.ps1")]
        public string InstallerPath { get; }

        [Option("--agent", Description = "Edge Agent image name, default is '" + DefaultAgentImage + "'")]
        public string AgentImage { get; } = DefaultAgentImage;

        [Option("--hub", Description = "Edge Hub image name, default is '" + DefaultHubImage + "'")]
        public string HubImage { get; } = DefaultHubImage;

        [Option("--temp-sensor", Description = "Simulated temp sensor image name, default is '" + DefaultSensorImage + "'")]
        public string SensorImage { get; } = DefaultSensorImage;

        [Option("--registry", Description = "Hostname[:port] of the container registry")]
        public string RegistryAddress { get; }

        [Option("--user", Description = "Username for container registry login")]
        public string RegistryUser { get; }

        Task<int> OnExecuteAsync()
        {
            string connectionString = EnvironmentVariable.Expect("E2E_IOT_HUB_CONNECTION_STRING");
            string endpoint = EnvironmentVariable.Expect("E2E_EVENT_HUB_ENDPOINT");

            Option<(string address, string username, string password)> registry =
                this.RegistryAddress != null && this.RegistryUser != null
                ? Option.Some((
                    this.RegistryAddress,
                    this.RegistryUser,
                    EnvironmentVariable.Expect("E2E_CONTAINER_REGISTRY_PASSWORD")
                  ))
                : Option.None<(string, string, string)>();

            return Profiler.Run(
                "Running tempSensor test",
                async () =>
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                    {
                        CancellationToken token = cts.Token;

                        // ** setup
                        var iotHub = new IotHub(connectionString, endpoint);
                        var device = await EdgeDevice.GetOrCreateIdentityAsync(
                            this.DeviceId, iotHub, token);

                        var daemon = new EdgeDaemon(this.InstallerPath);
                        await daemon.UninstallAsync(token);
                        await daemon.InstallAsync(device.ConnectionString, token);
                        await daemon.WaitForStatusAsync(EdgeDaemonStatus.Running, token);

                        var agent = new EdgeAgent(device.Id, iotHub);
                        await agent.WaitForStatusAsync(EdgeModuleStatus.Running, token);
                        await agent.PingAsync(token);

                        // ** test
                        var config = new EdgeConfiguration(device.Id, this.AgentImage, iotHub);
                        registry.ForEach(
                            r => config.AddRegistryCredentials(r.address, r.username, r.password)
                        );
                        config.AddEdgeHub(this.HubImage);
                        config.AddTempSensor(this.SensorImage);
                        await config.DeployAsync(token);

                        var hub = new EdgeModule("edgeHub", device.Id, iotHub);
                        var sensor = new EdgeModule("tempSensor", device.Id, iotHub);
                        await EdgeModule.WaitForStatusAsync(
                            new[] { hub, sensor }, EdgeModuleStatus.Running, token);
                        await sensor.WaitForEventsReceivedAsync(token);

                        var sensorTwin = new ModuleTwin(sensor.Id, device.Id, iotHub);
                        await sensorTwin.UpdateDesiredPropertiesAsync(new
                        {
                            properties = new
                            {
                                desired = new
                                {
                                    SendData = true,
                                    SendInterval = 10
                                }
                            }
                        }, token);
                        await sensorTwin.WaitForReportedPropertyUpdatesAsync(new
                        {
                            properties = new
                            {
                                reported = new
                                {
                                    SendData = true,
                                    SendInterval = 10
                                }
                            }
                        }, token);

                        // ** teardown
                        await daemon.StopAsync(token);
                        await device.MaybeDeleteIdentityAsync(token);
                    }

                    return 0;
                },
                "Completed tempSensor test"
            );
        }

        static Task<int> Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class RegistryAndUserOptionsMustBeSpecifiedTogether : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            if (value is Program obj)
            {
                if (obj.RegistryAddress == null ^ obj.RegistryUser == null)
                {
                    return new ValidationResult("--registry and --user must be specified together");
                }
            }
            return ValidationResult.Success;
        }
    }
}
