// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.EventHubs;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using EventHubClientTransportType = Microsoft.Azure.EventHubs.TransportType;
    using ServiceClientTransportType = Microsoft.Azure.Devices.TransportType;

    public class Details
    {
        public readonly Option<string> DeploymentFileName;

        public readonly Option<string> TwinTestFileName;

        const string DeployJson = @"
{
  ""modulesContent"": {
    ""$edgeAgent"": {
      ""properties.desired"": {
        ""schemaVersion"": ""1.0"",
        ""runtime"": {
          ""type"": ""docker"",
          ""settings"": {
            ""minDockerVersion"": ""v1.25"",
            ""loggingOptions"": """"<registry-info>
          }
        },
        ""systemModules"": {
          ""edgeAgent"": {
            ""type"": ""docker"",
            ""settings"": {
              ""image"": ""<image-edge-agent>"",
              ""createOptions"": """"
            }
          },
          ""edgeHub"": {
            ""type"": ""docker"",
            ""status"": ""running"",
            ""restartPolicy"": ""always"",
            ""settings"": {
              ""image"": ""<image-edge-hub>"",
              ""createOptions"": ""{\""HostConfig\"":{\""PortBindings\"":{\""8883/tcp\"":[{\""HostPort\"":\""8883\""}],\""443/tcp\"":[{\""HostPort\"":\""443\""}],\""5671/tcp\"":[{\""HostPort\"":\""5671\""}]}}}""
            },
		    ""env"": {
				""OptimizeForPerformance"": {
					""value"": ""<optimized-for-performance>""
				}
			},
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
            }
          }
        }
      }
    },
    ""$edgeHub"": {
      ""properties.desired"": {
        ""schemaVersion"": ""1.0"",
        ""routes"": {
          ""route"": ""FROM /* INTO $upstream""
        },
        ""storeAndForwardConfiguration"": {
          ""timeToLiveSecs"": 7200
        }
      }
    }
  }
}
";

        const string DeployJsonRegistry = @"
            ,""registryCredentials"": {
                ""registry"": {
                    ""address"": ""<registry-address>"",
                    ""username"": ""<registry-username>"",
                    ""password"": ""<registry-password>""
                }
            }
";

        readonly IBootstrapper bootstrapper;

        readonly Option<RegistryCredentials> credentials;

        readonly string iothubConnectionString;

        readonly string eventhubCompatibleEndpointWithEntityPath;

        readonly ServiceClientTransportType serviceClientTransportType;

        readonly EventHubClientTransportType eventHubClientTransportType;

        readonly string imageTag;

        readonly string deviceId;

        readonly string hostname;

        readonly string deviceCaCert;

        readonly string deviceCaPk;

        readonly string deviceCaCerts;

        readonly bool optimizedForPerformance;

        readonly LogLevel runtimeLogLevel;

        readonly bool cleanUpExistingDeviceOnSuccess;

        DeviceContext context;

        protected Details(
            IBootstrapper bootstrapper,
            Option<RegistryCredentials> credentials,
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            UpstreamProtocolType upstreamProtocol,
            string imageTag,
            string deviceId,
            string hostname,
            Option<string> deploymentFileName,
            Option<string> twinTestFileName,
            string deviceCaCert,
            string deviceCaPk,
            string deviceCaCerts,
            bool optimizedForPerformance,
            LogLevel runtimeLogLevel,
            bool cleanUpExistingDeviceOnSuccess)
        {
            this.bootstrapper = bootstrapper;
            this.credentials = credentials;
            this.iothubConnectionString = iothubConnectionString;
            this.eventhubCompatibleEndpointWithEntityPath = eventhubCompatibleEndpointWithEntityPath;

            switch (upstreamProtocol)
            {
                case UpstreamProtocolType.Amqp:
                case UpstreamProtocolType.Mqtt:
                    this.serviceClientTransportType = ServiceClientTransportType.Amqp;
                    this.eventHubClientTransportType = EventHubClientTransportType.Amqp;
                    break;

                case UpstreamProtocolType.AmqpWs:
                case UpstreamProtocolType.MqttWs:
                    this.serviceClientTransportType = ServiceClientTransportType.Amqp_WebSocket_Only;
                    this.eventHubClientTransportType = EventHubClientTransportType.AmqpWebSockets;
                    break;

                default:
                    throw new Exception($"Unexpected upstream protocol type {upstreamProtocol}");
            }

            this.imageTag = imageTag;
            this.deviceId = deviceId;
            this.hostname = hostname;
            this.DeploymentFileName = deploymentFileName;
            this.TwinTestFileName = twinTestFileName;
            this.deviceCaCert = deviceCaCert;
            this.deviceCaPk = deviceCaPk;
            this.deviceCaCerts = deviceCaCerts;
            this.optimizedForPerformance = optimizedForPerformance;
            this.runtimeLogLevel = runtimeLogLevel;
            this.cleanUpExistingDeviceOnSuccess = cleanUpExistingDeviceOnSuccess;
        }

        protected Task VerifyEdgeIsNotAlreadyActive()
        {
            Console.WriteLine("Verifying if edge is not already active.");
            return this.bootstrapper.VerifyNotActive();
        }

        protected Task VerifyBootstrapperDependencies()
        {
            Console.WriteLine("Verifying bootstrapper dependencies.");
            return this.bootstrapper.VerifyDependenciesAreInstalled();
        }

        protected Task InstallBootstrapper()
        {
            Console.WriteLine("Installing bootstrapper.");
            return this.bootstrapper.Install();
        }

        protected async Task GetOrCreateEdgeDeviceIdentity()
        {
            Console.WriteLine("Getting or Creating device Identity.");
            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.iothubConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(builder.ToString());

            Device device = await rm.GetDeviceAsync(this.deviceId);
            if (device != null)
            {
                Console.WriteLine($"Device '{device.Id}' already registered on IoT hub '{builder.HostName}'");
                Console.WriteLine($"Clean up Existing device? {this.cleanUpExistingDeviceOnSuccess}");

                this.context = new DeviceContext
                {
                    Device = device,
                    IotHubConnectionString = this.iothubConnectionString,
                    RegistryManager = rm,
                    RemoveDevice = this.cleanUpExistingDeviceOnSuccess
                };
            }
            else
            {
                await this.CreateEdgeDeviceIdentity(rm);
            }
        }

        protected Task ConfigureBootstrapper()
        {
            Console.WriteLine("Configuring bootstrapper.");
            IotHubConnectionStringBuilder builder =
                IotHubConnectionStringBuilder.Create(this.context.IotHubConnectionString);

            string connectionString =
                $"HostName={builder.HostName};" +
                $"DeviceId={this.context.Device.Id};" +
                $"SharedAccessKey={this.context.Device.Authentication.SymmetricKey.PrimaryKey}";

            return this.bootstrapper.Configure(connectionString, this.EdgeAgentImage(), this.hostname, this.deviceCaCert, this.deviceCaPk, this.deviceCaCerts, this.runtimeLogLevel);
        }

        protected Task StartBootstrapper()
        {
            Console.WriteLine("Starting bootstrapper.");
            return this.bootstrapper.Start();
        }

        protected Task VerifyEdgeAgentIsRunning()
        {
            Console.WriteLine("Verifying if edge Agent is running.");
            return this.bootstrapper.VerifyModuleIsRunning("edgeAgent");
        }

        protected async Task VerifyEdgeAgentIsConnectedToIotHub()
        {
            Console.WriteLine("Verifying if edge is connected to IoThub");
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300)))
            {
                Exception savedException = null;

                try
                {
                    ServiceClient serviceClient =
                        ServiceClient.CreateFromConnectionString(this.context.IotHubConnectionString, this.serviceClientTransportType);

                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);

                        try
                        {
                            CloudToDeviceMethodResult result = await serviceClient.InvokeDeviceMethodAsync(
                                this.context.Device.Id,
                                "$edgeAgent",
                                new CloudToDeviceMethod("ping"),
                                cts.Token);
                            if (result.Status == 200)
                            {
                                break;
                            }
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

        protected Task DeployToEdgeDevice()
        {
            Console.WriteLine("Deploying edge device.");
            (string deployJson, string[] modules) = this.DeploymentJson();

            Console.WriteLine($"Sending configuration to device '{this.context.Device.Id}' with modules:");
            foreach (string module in modules)
            {
                Console.WriteLine($"  {module}");
            }

            var config = JsonConvert.DeserializeObject<ConfigurationContent>(deployJson);
            return this.context.RegistryManager.ApplyConfigurationContentOnDeviceAsync(this.context.Device.Id, config);
        }

        protected async Task VerifyDataOnIoTHub(string moduleId)
        {
            Console.WriteLine($"Verifying data on IoTHub from {moduleId}");

            // First Verify if module is already running.
            await this.bootstrapper.VerifyModuleIsRunning(moduleId);

            var builder = new EventHubsConnectionStringBuilder(this.eventhubCompatibleEndpointWithEntityPath);
            builder.TransportType = this.eventHubClientTransportType;

            Console.WriteLine($"Receiving events from device '{this.context.Device.Id}' on Event Hub '{builder.EntityPath}'");

            EventHubClient eventHubClient =
                EventHubClient.CreateFromConnectionString(builder.ToString());

            PartitionReceiver eventHubReceiver = eventHubClient.CreateReceiver(
                "$Default",
                EventHubPartitionKeyResolver.ResolveToPartition(
                    this.context.Device.Id,
                    (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                EventPosition.FromEnd());

            var result = new TaskCompletionSource<bool>();
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
            {
                using (cts.Token.Register(() => result.TrySetCanceled()))
                {
                    eventHubReceiver.SetReceiveHandler(
                        new PartitionReceiveHandler(
                            eventData =>
                            {
                                eventData.SystemProperties.TryGetValue("iothub-connection-device-id", out object devId);
                                eventData.SystemProperties.TryGetValue("iothub-connection-module-id", out object modId);

                                if (devId != null && devId.ToString().Equals(this.context.Device.Id) &&
                                    modId != null && modId.ToString().Equals(moduleId))
                                {
                                    result.TrySetResult(true);
                                    return true;
                                }

                                return false;
                            }));

                    await result.Task;
                }
            }

            Console.WriteLine("VerifyDataOnIoTHub completed.");
            await eventHubReceiver.CloseAsync();
            await eventHubClient.CloseAsync();
        }

        protected async Task VerifyTwinAsync()
        {
            await this.TwinTestFileName.ForEachAsync(
                async fileName =>
                {
                    Console.WriteLine($"VerifyTwinAsync for {fileName} started.");
                    string twinTestJson = File.ReadAllText(fileName);

                    var twinTest = JsonConvert.DeserializeObject<TwinTestConfiguration>(twinTestJson);

                    Twin currentTwin = await this.context.RegistryManager.GetTwinAsync(this.context.Device.Id, twinTest.ModuleId);

                    if (twinTest.Properties?.Desired?.Count > 0)
                    {
                        // Build Patch Object.
                        string patch = JsonConvert.SerializeObject(twinTest, Formatting.Indented);
                        await this.context.RegistryManager.UpdateTwinAsync(this.context.Device.Id, twinTest.ModuleId, patch, currentTwin.ETag);
                    }

                    if (twinTest.Properties?.Reported?.Count > 0)
                    {
                        TimeSpan retryInterval = TimeSpan.FromSeconds(10);
                        bool IsValid(TwinCollection currentTwinReportedProperty) => twinTest.Properties.Reported.Cast<KeyValuePair<string, object>>().All(p => currentTwinReportedProperty.Cast<KeyValuePair<string, object>>().Contains(p));

                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                        {
                            async Task<TwinCollection> Func()
                            {
                                // Removing reSharper warning for CTS, Code Block will never exit before the delegate code completes because of using.
                                // ReSharper disable AccessToDisposedClosure
                                currentTwin = await this.context.RegistryManager.GetTwinAsync(this.context.Device.Id, twinTest.ModuleId, cts.Token);
                                // ReSharper restore AccessToDisposedClosure
                                return await Task.FromResult(currentTwin.Properties.Reported);
                            }

                            await Retry.Do(Func, IsValid, null, retryInterval, cts.Token);
                        }
                    }

                    Console.WriteLine($"VerifyTwinAsync for {fileName} completed.");
                });
        }

        protected Task RemoveTempSensorFromEdgeDevice()
        {
            Console.WriteLine("Removing tempSensor from edge device.");
            (string deployJson, string[] _) = this.DeploymentJson();

            var config = JsonConvert.DeserializeObject<ConfigurationContent>(deployJson);
            JObject desired = JObject.FromObject(config.ModulesContent["$edgeAgent"]["properties.desired"]);
            if (desired.TryGetValue("modules", out JToken modules))
            {
                IList<JToken> removeList = new List<JToken>();
                foreach (JToken module in modules.Children())
                {
                    removeList.Add(module);
                }

                foreach (JToken module in removeList)
                {
                    module.Remove();
                }
            }

            config.ModulesContent["$edgeAgent"]["properties.desired"] = desired;

            return this.context.RegistryManager.ApplyConfigurationContentOnDeviceAsync(this.context.Device.Id, config);
        }

        protected Task StopBootstrapper()
        {
            Console.WriteLine("Stopping bootstrapper.");
            return this.bootstrapper.Stop();
        }

        protected Task ResetBootstrapper()
        {
            Console.WriteLine("Resetting bootstrapper.");
            return this.bootstrapper.Reset();
        }

        protected void KeepEdgeDeviceIdentity()
        {
            Console.WriteLine("Keeping Edge Device Identity.");
            if (this.context != null)
            {
                this.context.RemoveDevice = false;
            }
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
                    Console.WriteLine($"Trying to remove device from Registry. Device Id: {device.Id}");
                    return this.context.RegistryManager.RemoveDeviceAsync(device);
                }
            }

            return Task.CompletedTask;
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

        string EdgeAgentImage()
        {
            return this.BuildImageName("azureiotedge-agent");
        }

        string EdgeHubImage()
        {
            return this.BuildImageName("azureiotedge-hub");
        }

        string TempSensorImage()
        {
            return this.BuildImageName("azureiotedge-simulated-temperature-sensor");
        }

        string BuildImageName(string name)
        {
            string prefix = this.credentials.Match(c => $"{c.Address}/microsoft", () => "mcr.microsoft.com");
            return $"{prefix}/{name}:{this.imageTag}";
        }

        (string, string[]) DeploymentJson()
        {
            string edgeAgentImage = this.EdgeAgentImage();
            string edgeHubImage = this.EdgeHubImage();
            string tempSensorImage = this.TempSensorImage();
            string deployJson = this.DeploymentFileName.Match(
                f =>
                {
                    Console.WriteLine($"Deployment file used: {f}");
                    return JObject.Parse(File.ReadAllText(f)).ToString();
                },
                () =>
                {
                    string deployJsonRegistry = this.credentials.Match(
                        c =>
                        {
                            string jsonRegistry = DeployJsonRegistry;
                            jsonRegistry = Regex.Replace(jsonRegistry, "<registry-address>", c.Address);
                            jsonRegistry = Regex.Replace(jsonRegistry, "<registry-username>", c.User);
                            jsonRegistry = Regex.Replace(jsonRegistry, "<registry-password>", c.Password);
                            return jsonRegistry;
                        },
                        () => string.Empty);

                    string json = DeployJson;
                    json = Regex.Replace(json, "<image-edge-agent>", edgeAgentImage);
                    json = Regex.Replace(json, "<image-edge-hub>", edgeHubImage);
                    json = Regex.Replace(json, "<image-temp-sensor>", tempSensorImage);
                    json = Regex.Replace(json, "<registry-info>", deployJsonRegistry);
                    json = Regex.Replace(json, "<optimized-for-performance>", this.optimizedForPerformance.ToString());
                    return json;
                });

            return (deployJson, new[] { edgeAgentImage, edgeHubImage, tempSensorImage });
        }
    }

    public class DeviceContext
    {
        public Device Device;
        public string IotHubConnectionString;
        public RegistryManager RegistryManager;
        public bool RemoveDevice;
    }
}
