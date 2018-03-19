// Copyright (c) Microsoft. All rights reserved.

namespace IotEdgeQuickstart
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.EventHubs;
    using Newtonsoft.Json;
    using RunProcessAsTask;

    public class Details
    {
        readonly string iotedgectlArchivePath;
        readonly string iothubConnectionString;
        readonly string eventhubCompatibleEndpointWithEntityPath;
        readonly string registryAddress;
        readonly string registryUser;
        readonly string registryPassword;
        readonly string imageTag;
        readonly string deviceId;
        readonly string hostname;

        DeviceContext context;

        protected Details(
            string iotedgectlArchivePath,
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            string registryAddress,
            string registryUser,
            string registryPassword,
            string imageTag,
            string deviceId,
            string hostname
            )
        {
            this.iotedgectlArchivePath = iotedgectlArchivePath;
            this.iothubConnectionString = iothubConnectionString;
            this.registryAddress = registryAddress;
            this.registryUser = registryUser;
            this.registryPassword = registryPassword;
            this.eventhubCompatibleEndpointWithEntityPath = eventhubCompatibleEndpointWithEntityPath;
            this.imageTag = imageTag;
            this.deviceId = deviceId;
            this.hostname = hostname;
        }

        protected static async Task VerifyEdgeIsNotAlreadyInstalled()
        {
            try
            {
                await RunProcessAsync("iotedgectl", "status");
            }
            catch (Win32Exception)
            {
                // Should fail for one of two reasons:
                // 1. [ExitCode == 9009] iotedgectl isn't installed
                // 2. [ExitCode == 1] `iotedgectl status` failed because there's no config
                return;
            }

            throw new Exception("IoT Edge runtime is installed. Run `iotedgectl uninstall` before running this test.");
        }

        protected static Task VerifyDockerIsInstalled()
        {
            return RunProcessAsync("docker", "--version");
        }

        protected static Task VerifyPipIsInstalled()
        {
            return RunProcessAsync("pip", "--version");
        }

        protected Task InstallIotedgectl()
        {
            const string PackageName = "azure-iot-edge-runtime-ctl";
            string archivePath = this.iotedgectlArchivePath;

            Console.WriteLine($"Installing python package '{PackageName}' from {archivePath ?? "pypi"}");

            return RunProcessAsync(
                "pip",
                $"install --disable-pip-version-check --upgrade {archivePath ?? PackageName}",
                300); // 5 min timeout because install can be slow on raspberry pi
        }

        protected async Task GetOrCreateEdgeDeviceIdentity()
        {
            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.iothubConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(builder.ToString());

            Device device = await rm.GetDeviceAsync(this.deviceId);
            if (device != null)
            {
                Console.WriteLine($"Device '{device.Id}' already registered on IoT hub '{builder.HostName}'");

                this.context = new DeviceContext
                {
                    Device = device,
                    IotHubConnectionString = this.iothubConnectionString,
                    RegistryManager = rm,
                    RemoveDevice = false
                };
            }
            else
            {
                await this.CreateEdgeDeviceIdentity(rm);
            }
        }

        async Task CreateEdgeDeviceIdentity(RegistryManager rm)
        {
            var device = new Device(this.deviceId)
            {
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas },
                Capabilities = new DeviceCapabilities() { IotEdge = true }
            };

            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.iothubConnectionString);
            Console.WriteLine($"Registering device '{device.Id}' on IoT hub '{builder.HostName}'");

            device = await rm.AddDeviceAsync(device);

            this.context = new DeviceContext
            {
                Device = device,
                IotHubConnectionString = this.iothubConnectionString,
                RegistryManager = rm,
                RemoveDevice = true
            };
        }

        protected async Task IotedgectlSetup()
        {
            string registryArgs =
                this.registryAddress != null && this.registryUser != null && this.registryPassword != null
                ? $"--docker-registries {this.registryAddress} {this.registryUser} {this.registryPassword}"
                : string.Empty;

            Console.WriteLine($"Setting up iotedgectl with container registry '{(registryArgs != string.Empty ? this.registryAddress : "<none>")}'");

            IotHubConnectionStringBuilder builder =
                IotHubConnectionStringBuilder.Create(this.context.IotHubConnectionString);

            string deviceConnectionString =
                $"HostName={builder.HostName};" +
                $"DeviceId={this.context.Device.Id};" +
                $"SharedAccessKey={this.context.Device.Authentication.SymmetricKey.PrimaryKey}";

            await RunProcessAsync(
                "iotedgectl",
                $"setup --connection-string \"{deviceConnectionString}\" --nopass {registryArgs} --image {this.EdgeAgentImage()} --edge-hostname {this.hostname}",
                60);
        }

        protected static Task IotedgectlStart()
        {
            return RunProcessAsync("iotedgectl", "start", 120);
        }

        protected static Task VerifyEdgeAgentIsRunning()
        {
            return VerifyDockerContainerIsRunning("edgeAgent");
        }

        protected async Task VerifyEdgeAgentIsConnectedToIotHub()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                Exception savedException = null;

                try
                {
                    ServiceClient serviceClient =
                        ServiceClient.CreateFromConnectionString(this.context.IotHubConnectionString);

                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

                        try
                        {
                            CloudToDeviceMethodResult result = await serviceClient.InvokeDeviceMethodAsync(
                                this.context.Device.Id,
                                "$edgeAgent",
                                new CloudToDeviceMethod("ping"),
                                cts.Token);
                            if (result.Status == 200) break;
                        }
                        catch (Exception e)
                        {
                            savedException = e;
                        }
                    }
                }
                catch (OperationCanceledException e)
                {
                    throw new Exception($"Failed to ping $edgeAgent from the cloud: {savedException?.Message ?? e.Message}");
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to ping $edgeAgent from the cloud: {e.Message}");
                }
            }
        }

        protected Task DeployTempSensorToEdgeDevice()
        {
            string deployJson = DeployJson;
            string edgeAgentImage = this.EdgeAgentImage();
            string edgeHubImage = this.EdgeHubImage();
            string tempSensorImage = this.TempSensorImage();

            Console.WriteLine($"Sending configuration to device '{this.context.Device.Id}' with modules:");
            Console.WriteLine($"  {edgeAgentImage}\n  {edgeHubImage}\n  {tempSensorImage}");

            deployJson = Regex.Replace(deployJson, "<image-edge-agent>", edgeAgentImage);
            deployJson = Regex.Replace(deployJson, "<image-edge-hub>", edgeHubImage);
            deployJson = Regex.Replace(deployJson, "<image-temp-sensor>", tempSensorImage);

            var config = JsonConvert.DeserializeObject<ConfigurationContent>(deployJson);
            return this.context.RegistryManager.ApplyConfigurationContentOnDeviceAsync(this.context.Device.Id, config);
        }

        protected static Task VerifyTempSensorIsRunning()
        {
            return VerifyDockerContainerIsRunning("tempSensor");
        }

        protected async Task VerifyTempSensorIsSendingDataToIotHub()
        {
            var builder = new EventHubsConnectionStringBuilder(this.eventhubCompatibleEndpointWithEntityPath);

            Console.WriteLine($"Receiving events from device '{this.context.Device.Id}' on Event Hub '{builder.EntityPath}'");

            EventHubClient eventHubClient =
                EventHubClient.CreateFromConnectionString(builder.ToString());

            PartitionReceiver eventHubReceiver = eventHubClient.CreateReceiver(
                "$Default",
                EventHubPartitionKeyResolver.ResolveToPartition(
                    this.context.Device.Id,
                    (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                DateTime.Now);

            var result = new TaskCompletionSource<bool>();
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                using (cts.Token.Register(() => result.TrySetCanceled()))
                {
                    eventHubReceiver.SetReceiveHandler(new PartitionReceiveHandler(eventData =>
                    {
                        eventData.Properties.TryGetValue("iothub-connection-device-id", out object devId);
                        eventData.Properties.TryGetValue("iothub-connection-module-id", out object modId);

                        if (devId != null && devId.ToString() == this.context.Device.Id &&
                            modId != null && modId.ToString() == "tempSensor")
                        {
                            result.TrySetResult(true);
                            return true;
                        }

                        return false;
                    }));

                    await result.Task;
                }
            }

            await eventHubReceiver.CloseAsync();
            await eventHubClient.CloseAsync();
        }

        protected static Task IotedgectlStop()
        {
            return RunProcessAsync("iotedgectl", "stop", 60);
        }

        protected static Task IotedgectlUninstall()
        {
            return RunProcessAsync("iotedgectl", "uninstall", 60);
        }

        protected Task MaybeDeleteEdgeDeviceIdentity()
        {
            if (this.context != null)
            {
                Device device = this.context.Device;
                bool remove = this.context.RemoveDevice;
                this.context.Device = null;

                if (remove)
                {
                    return this.context.RegistryManager.RemoveDeviceAsync(device);
                }
            }

            return Task.CompletedTask;
        }

        string EdgeAgentImage()
        {
            return this.BuildImageName("microsoft/azureiotedge-agent");
        }

        string EdgeHubImage()
        {
            return this.BuildImageName("microsoft/azureiotedge-hub");
        }

        string TempSensorImage()
        {
            return this.BuildImageName("microsoft/azureiotedge-simulated-temperature-sensor");
        }

        string BuildImageName(string name)
        {
            string address = this.registryAddress;
            string registry = address == null ? string.Empty : $"{address}/";

            return $"{registry}{name}:{this.imageTag}";
        }

        static async Task VerifyDockerContainerIsRunning(string name)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
            {
                string errorMessage = null;

                try
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

                        string status = await RunProcessAsync(
                            "docker",
                            $"ps --quiet --filter \"name = {name}\"",
                            cts.Token);

                        if (status.Trim() != string.Empty) break;

                        errorMessage = "Not found";
                    }
                }
                catch (OperationCanceledException e)
                {
                    throw new Exception($"Error searching for {name} module: {errorMessage ?? e.Message}");
                }
                catch (Exception e)
                {
                    throw new Exception($"Error searching for {name} module: {e.Message}");
                }
            }
        }

        static async Task<string> RunProcessAsync(string name, string args, int timeoutSeconds = 15)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                return await RunProcessAsync(name, args, cts.Token);
            }
        }

        static async Task<string> RunProcessAsync(string name, string args, CancellationToken token)
        {
            var info = new ProcessStartInfo
            {
                FileName = name,
                Arguments = args,
            };

            using (ProcessResults result = await ProcessEx.RunAsync(info, token))
            {
                if (result.ExitCode != 0)
                {
                    throw new Win32Exception(result.ExitCode, $"'{name}' failed with: {string.Join("\n", result.StandardError)}");
                }

                return string.Join("\n", result.StandardOutput);
            }
        }

        const string DeployJson = @"
{
  ""moduleContent"": {
    ""$edgeAgent"": {
      ""properties.desired"": {
        ""schemaVersion"": ""1.0"",
        ""runtime"": {
          ""type"": ""docker"",
          ""settings"": {
            ""minDockerVersion"": ""v1.13"",
            ""loggingOptions"": """"
          }
        },
        ""systemModules"": {
          ""edgeAgent"": {
            ""type"": ""docker"",
            ""settings"": {
              ""image"": ""<image-edge-agent>"",
              ""createOptions"": """"
            },
            ""configuration"": {
              ""id"": ""1234""
            }
          },
          ""edgeHub"": {
            ""type"": ""docker"",
            ""status"": ""running"",
            ""restartPolicy"": ""always"",
            ""settings"": {
              ""image"": ""<image-edge-hub>"",
              ""createOptions"": """"
            },
            ""configuration"": {
              ""id"": ""1234""
            }
          }
        },
        ""modules"": {
          ""tempSensor"": {
            ""version"": ""1.0"",
            ""type"": ""docker"",
            ""status"": ""running"",
            ""restartPolicy"": ""always"",
            ""settings"": {
              ""image"": ""<image-temp-sensor>"",
              ""createOptions"": """"
            },
            ""configuration"": {
              ""id"": ""1234""
            }
          }
        }
      }
    },
    ""$edgeHub"": {
      ""properties.desired"": {
        ""schemaVersion"": ""1.0"",
        ""routes"": {
          ""route1"": ""FROM /* INTO $upstream""
        },
        ""storeAndForwardConfiguration"": {
          ""timeToLiveSecs"": 90000
        }
      }
    }
  }
}
";
    }

    public class DeviceContext
    {
        public Device Device;
        public string IotHubConnectionString;
        public RegistryManager RegistryManager;
        public bool RemoveDevice;
    }

    class PartitionReceiveHandler : IPartitionReceiveHandler
    {
        readonly Func<EventData, bool> onEventReceived;
        public PartitionReceiveHandler(Func<EventData, bool> onEventReceived)
        {
            this.onEventReceived = onEventReceived;
        }
        public Task ProcessEventsAsync(IEnumerable<EventData> events)
        {
            if (events != null)
            {
                foreach (EventData @event in events)
                {
                    if (this.onEventReceived(@event)) break;
                }
            }
            return Task.CompletedTask;
        }
        public Task ProcessErrorAsync(Exception error) => throw error;
        public int MaxBatchSize { get; } = 10;
    }
}
