// Copyright (c) Microsoft. All rights reserved.

namespace IotEdgeQuickstart
{
    using System;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    [Command(
        Name = "IotEdgeQuickstart",
        Description = "An app which automates the \"Quickstart\" tutorial (https://docs.microsoft.com/en-us/azure/iot-edge/quickstart-linux)")]
    [HelpOption]
    class Program
    {
        static int Main(string[] args) => CommandLineApplication.ExecuteAsync<Program>(args).Result;

        [Option("-a|--iotedgectl-archive <path>", Description = "Path to python 'azure-iot-edge-runtime-ctl' archive, overrides environment variable 'iotedgectlArchivePath'. Default is no path (archive is installed from pypi).")]
        public string IotedgectlArchivePath { get; } = Environment.GetEnvironmentVariable("iotedgectlArchivePath");

        [Option("-c|--connection-string <value>", Description = "IoT Hub connection string (hub-scoped, e.g. iothubowner), overrides environment variable 'iothubConnectionString'. Default is to look in Key Vault.")]
        public string IotHubConnectionString { get; } = Environment.GetEnvironmentVariable("iothubConnectionString");

        [Option("-e|--eventhub-endpoint <value>", Description = "Event Hub-compatible endpoint for IoT Hub, including EntityPath, overrides environment variable 'eventhubCompatibleEndpointWithEntityPath'. Default is to look in Key Vault.")]
        public string EventHubCompatibleEndpointWithEntityPath { get; } = Environment.GetEnvironmentVariable("eventhubCompatibleEndpointWithEntityPath");

        [Option("-r|--registry <hostname>", Description = "Hostname of Docker registry used to pull images, overrides environment variable 'registryAddress'. Default is to use DockerHub with anonymous access. If a registry address is specified, but no username or password, the behavior is to look in Key Vault.")]
        public string RegistryAddress { get; } = Environment.GetEnvironmentVariable("registryAddress");

        [Option("-u|--username <username>", Description = "Docker registry username, overrides environment variable 'registryUser'. Default is anonymous access (no username).")]
        public string RegistryUser { get; } = Environment.GetEnvironmentVariable("registryUser");

        [Option("-p|--password <password>", Description = "Docker registry password, overrides environment variable 'registryPassword'. Default is anonymous access (no password).")]
        public string RegistryPassword { get; } = Environment.GetEnvironmentVariable("registryPassword");

        [Option("-t|--tag <value>", Description = "Tag to append when pulling images, overrides environment variable 'imageTag'. Default is '1.0-preview'.")]
        public string ImageTag { get; } = Environment.GetEnvironmentVariable("imageTag");

        [Option("-d|--device-id", Description = "Edge device identifier registered with IoT Hub. Default is an auto-generated unique identifier.")]
        public string DeviceId { get; } = $"iot-edge-quickstart-{Guid.NewGuid()}";

        [Option("-n|--edge-hostname", Description = "Edge device's hostname. Default is the fixed name \"quickstart\".")]
        public string EdgeHostname { get; } = "quickstart";

        [Option("--leave-running=<All/Core/None>", CommandOptionType.SingleOrNoValue, Description = "Leave IoT Edge running when the app is finished. Default is 'none' (and corresponding identities in IoT Hub will be removed). If given as a switch, assumes 'all'.")]
        public LeaveRunning LeaveRunning { get; } = LeaveRunning.None;

        // ReSharper disable once UnusedMember.Local
        async Task<int> OnExecuteAsync()
        {
            try
            {
                string connectionString = this.IotHubConnectionString ??
                    await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");

                string endpoint = this.EventHubCompatibleEndpointWithEntityPath ??
                    await SecretsHelper.GetSecretFromConfigKey("eventHubConnStrKey");

                string address = this.RegistryAddress;
                string user = this.RegistryUser;
                string password = this.RegistryPassword;

                if (address != null && user == null && password == null)
                {
                    (user, password) = await this.RegistryArgsFromSecret(address);
                }

                string tag = this.ImageTag ?? "1.0-preview";

                var test = new Quickstart(
                    this.IotedgectlArchivePath,
                    connectionString,
                    endpoint,
                    address,
                    user,
                    password,
                    tag,
                    this.DeviceId,
                    this.EdgeHostname,
                    this.LeaveRunning);
                await test.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 1;
            }

            Console.WriteLine("Success!");
            return 0;
        }

        async Task<(string, string)> RegistryArgsFromSecret(string address)
        {
            // Expects our Key Vault to contain a secret with the following properties:
            //  key   - based on registry hostname (e.g.,
            //          edgerelease.azurecr.io => edgerelease-azurecr-io)
            //  value - "<user> <password>" (separated by a space)

            string key = address.Replace('.', '-');
            string value = await SecretsHelper.GetSecret(key);
            string[] vals = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return (vals[0], vals[1]);
        }
    }

    public enum LeaveRunning
    {
        All,  // don't clean up anything
        Core, // remove modules/identities except Edge Agent & Hub
        None  // iotedgectl stop, uninstall, remove device identity
    }
}
