// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.TempSensor
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.Devices.Edge.Test.Common;
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
")]
    [HelpOption]
    [RegistryAndUserOptionsMustBeSpecifiedTogether]
    [InstallerPathIsRequiredOnWindowsInvalidOnLinux]
    class Program
    {
        const string DefaultAgentImage = "mcr.microsoft.com/azureiotedge-agent:1.0";
        const string DefaultHubImage = "mcr.microsoft.com/azureiotedge-hub:1.0";
        const string DefaultSensorImage = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0";
        const int DefaultTimeout = 5;

        [Required]
        [Argument(0, Name = "device-id", Description = "Device ID")]
        public string DeviceId { get; }

        [DirectoryExists]
        [Option("--installer-path", Description = "Path to Windows installer script (IotEdgeSecurityDaemon.ps1)")]
        public string InstallerPath { get; }

        [DirectoryExists]
        [Option("--package-path", Description = "Path to installation packages")]
        public string PackagesPath { get; }

        [Option("--proxy", Description = "HTTPS proxy for communication with IoT Hub")]
        public Uri Proxy { get; }

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

        [Option("--timeout", Description = "Time to wait, in minutes, before cancelling the test, default is '5'")]
        public int Timeout { get; } = DefaultTimeout;

        [Option("--verbose", Description = "Output more information to the console")]
        public bool Verbose { get; }

        [Option("--logfile", Description = "Also log verbose information to a file")]
        public string LogFile { get; }

        static Task<int> Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args);

        Task<int> OnExecuteAsync()
        {
            var test = new Test();
            return test.RunAsync(
                new Test.Args
                {
                    DeviceId = this.DeviceId,
                    ConnectionString = EnvironmentVariable.Expect("E2E_IOT_HUB_CONNECTION_STRING"),
                    Endpoint = EnvironmentVariable.Expect("E2E_EVENT_HUB_ENDPOINT"),
                    InstallerPath = this.InstallerPath,
                    PackagesPath = Option.Maybe(this.PackagesPath),
                    Proxy = Option.Maybe(this.Proxy),
                    AgentImage = this.AgentImage,
                    HubImage = this.HubImage,
                    SensorImage = this.SensorImage,
                    Registry = this.RegistryAddress != null
                        ? Option.Some(
                            (
                                this.RegistryAddress,
                                this.RegistryUser,
                                EnvironmentVariable.Expect("E2E_CONTAINER_REGISTRY_PASSWORD")))
                        : Option.None<(string, string, string)>(),
                    Timeout = TimeSpan.FromMinutes(this.Timeout),
                    Verbose = this.Verbose,
                    LogFile = Option.Maybe(this.LogFile)
                });
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RegistryAndUserOptionsMustBeSpecifiedTogether : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            if (value is Program obj)
            {
                if ((obj.RegistryAddress == null) ^ (obj.RegistryUser == null))
                {
                    return new ValidationResult("--registry and --user must be specified together");
                }
            }

            return ValidationResult.Success;
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InstallerPathIsRequiredOnWindowsInvalidOnLinux : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            if (value is Program obj)
            {
                if (obj.InstallerPath == null && Platform.IsWindows())
                {
                    return new ValidationResult("--installer-path is required on Windows");
                }

                if (obj.InstallerPath != null && !Platform.IsWindows())
                {
                    return new ValidationResult("--installer-path can only be specified on Windows");
                }
            }

            return ValidationResult.Success;
        }
    }
}
