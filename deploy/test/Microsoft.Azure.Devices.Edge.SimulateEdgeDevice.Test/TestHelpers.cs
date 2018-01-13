// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.SimulateEdgeDevice.Test
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.EventHubs;
    using Newtonsoft.Json;
    using RunProcessAsTask;

    static class TestHelpers
    {
        public static async Task VerifyEdgeIsNotAlreadyInstalled()
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

        public static Task VerifyDockerIsInstalled()
        {
            return RunProcessAsync("docker", "--version");
        }

        public static Task VerifyPipIsInstalled()
        {
            return RunProcessAsync("pip", "--version");
        }

        public static Task InstallIotedgectl()
        {
            // TODO: install from tar.gz if test is called with special args

            return RunProcessAsync(
                "pip",
                "install --disable-pip-version-check --upgrade azure-iot-edge-runtime-ctl",
                120);
        }

        public static async Task<DeviceContext> RegisterNewEdgeDeviceAsync()
        {
            var device = new Device($"simulate-edge-device-test-{Guid.NewGuid()}")
            {
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas },
                Capabilities = new DeviceCapabilities() { IotEdge = true }
            };

            string iotHubConnectionString =
                Environment.GetEnvironmentVariable("iothubConnectionString") ??
                await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            RegistryManager rm = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            device = await rm.AddDeviceAsync(device);

            return new DeviceContext
            {
                Device = device,
                IotHubConnectionString = iotHubConnectionString,
                RegistryManager = rm
            };
        }

        public static Task IotedgectlSetup(DeviceContext context)
        {
            // TODO: add ACR credentials if test is called with special args

            IotHubConnectionStringBuilder builder =
                IotHubConnectionStringBuilder.Create(context.IotHubConnectionString);

            string deviceConnectionString =
                $"HostName={builder.HostName};" +
                $"DeviceId={context.Device.Id};" +
                $"SharedAccessKey={context.Device.Authentication.SymmetricKey.PrimaryKey}";

            return RunProcessAsync(
                "iotedgectl",
                $"setup --connection-string \"{deviceConnectionString}\" --auto-cert-gen-force-no-passwords",
                60);
        }

        public static Task IotedgectlStart()
        {
            return RunProcessAsync("iotedgectl", "start", 120);
        }

        public static Task VerifyEdgeAgentIsRunning()
        {
            return VerifyDockerContainerIsRunning("edgeAgent");
        }

        public static async Task VerifyEdgeAgentIsConnectedToIotHub(DeviceContext context)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                Exception savedException = null;

                try
                {
                    ServiceClient serviceClient =
                        ServiceClient.CreateFromConnectionString(context.IotHubConnectionString);

                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);

                        try
                        {
                            CloudToDeviceMethodResult result = await serviceClient.InvokeDeviceMethodAsync(
                                context.Device.Id,
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

        public static Task DeployTempSensorToEdgeDevice(DeviceContext context)
        {
            var config = JsonConvert.DeserializeObject<ConfigurationContent>(DeployJson);
            return context.RegistryManager.ApplyConfigurationContentOnDeviceAsync(context.Device.Id, config);
        }

        public static Task VerifyTempSensorIsRunning()
        {
            return VerifyDockerContainerIsRunning("tempSensor");
        }

        public static async Task VerifyTempSensorIsSendingDataToIotHub(DeviceContext context)
        {
            string eventHubConnectionString =
                Environment.GetEnvironmentVariable("eventhubCompatibleEndpointWithEntityPath") ??
                await SecretsHelper.GetSecretFromConfigKey("eventHubConnStrKey");

            EventHubClient eventHubClient =
                EventHubClient.CreateFromConnectionString(eventHubConnectionString);

            PartitionReceiver eventHubReceiver = eventHubClient.CreateReceiver(
                "$Default",
                EventHubPartitionKeyResolver.ResolveToPartition(
                    context.Device.Id,
                    (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                DateTime.Now);

            var result = new TaskCompletionSource<bool>();
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                using (cts.Token.Register(() => result.TrySetCanceled()))
                {
                    eventHubReceiver.SetReceiveHandler(new PartitionReceiveHandler(eventData =>
                    {
                        eventData.Properties.TryGetValue("iothub-connection-device-id", out object devId);
                        eventData.Properties.TryGetValue("iothub-connection-module-id", out object modId);

                        if (devId != null && devId.ToString() == context.Device.Id &&
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

        public static Task IotedgectlStop()
        {
            return RunProcessAsync("iotedgectl", "stop", 60);
        }

        public static Task IotedgectlUninstall()
        {
            return RunProcessAsync("iotedgectl", "uninstall", 60);
        }

        public static Task UnregisterEdgeDevice(DeviceContext context)
        {
            return context != null
                ? context.RegistryManager.RemoveDeviceAsync(context.Device)
                : Task.CompletedTask;
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

                        errorMessage = $"Not found";
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

        // TODO: Image names for agent, hub, tempSensor should not be hard-coded to 1.0-preview
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
              ""image"": ""microsoft/azureiotedge-agent:1.0-preview"",
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
              ""image"": ""microsoft/azureiotedge-hub:1.0-preview"",
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
              ""image"": ""microsoft/azureiotedge-simulated-temperature-sensor:1.0-preview"",
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

    class DeviceContext
    {
        public Device Device;
        public string IotHubConnectionString;
        public RegistryManager RegistryManager;
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
