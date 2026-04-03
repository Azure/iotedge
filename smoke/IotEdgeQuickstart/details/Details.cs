// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.EventHubs;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using EventHubClientTransportType = Microsoft.Azure.EventHubs.TransportType;
    using RetryPolicy = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.RetryPolicy;

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
				},
				""NestedEdgeEnabled"": {
					""value"": ""false""
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

        readonly Option<DPSAttestation> dpsAttestation;

        readonly string eventhubCompatibleEndpointWithEntityPath;

        readonly EventHubClientTransportType eventHubClientTransportType;

        readonly string imageTag;

        readonly string deviceId;

        readonly string hostname;

        readonly Option<string> parentHostname;

        readonly Option<string> parentEdgeDevice;

        readonly string deviceCaCert;

        readonly string deviceCaPk;

        readonly string deviceCaCerts;

        readonly bool optimizedForPerformance;

        readonly bool initializeWithAgentArtifact;

        readonly LogLevel runtimeLogLevel;

        readonly bool cleanUpExistingDeviceOnSuccess;

        DeviceContext context;

        Option<IWebProxy> proxy;

        protected Details(
            IBootstrapper bootstrapper,
            Option<RegistryCredentials> credentials,
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            UpstreamProtocolType upstreamProtocol,
            Option<string> proxy,
            string imageTag,
            string deviceId,
            string hostname,
            Option<string> parentHostname,
            Option<string> parentEdgeDevice,
            Option<string> deploymentFileName,
            Option<string> twinTestFileName,
            string deviceCaCert,
            string deviceCaPk,
            string deviceCaCerts,
            bool optimizedForPerformance,
            bool initializeWithAgentArtifact,
            LogLevel runtimeLogLevel,
            bool cleanUpExistingDeviceOnSuccess,
            Option<DPSAttestation> dpsAttestation)
        {
            this.bootstrapper = bootstrapper;
            this.credentials = credentials;
            this.iothubConnectionString = iothubConnectionString;
            this.dpsAttestation = dpsAttestation;
            this.eventhubCompatibleEndpointWithEntityPath = eventhubCompatibleEndpointWithEntityPath;

            switch (upstreamProtocol)
            {
                case UpstreamProtocolType.Amqp:
                case UpstreamProtocolType.Mqtt:
                    this.eventHubClientTransportType = EventHubClientTransportType.Amqp;
                    break;

                case UpstreamProtocolType.AmqpWs:
                case UpstreamProtocolType.MqttWs:
                    this.eventHubClientTransportType = EventHubClientTransportType.AmqpWebSockets;
                    break;

                default:
                    throw new Exception($"Unexpected upstream protocol type {upstreamProtocol}");
            }

            this.imageTag = imageTag;
            this.deviceId = deviceId;
            this.hostname = hostname;
            this.parentHostname = parentHostname;
            this.parentEdgeDevice = parentEdgeDevice;
            this.DeploymentFileName = deploymentFileName;
            this.TwinTestFileName = twinTestFileName;
            this.deviceCaCert = deviceCaCert;
            this.deviceCaPk = deviceCaPk;
            this.deviceCaCerts = deviceCaCerts;
            this.optimizedForPerformance = optimizedForPerformance;
            this.initializeWithAgentArtifact = initializeWithAgentArtifact;
            this.runtimeLogLevel = runtimeLogLevel;
            this.cleanUpExistingDeviceOnSuccess = cleanUpExistingDeviceOnSuccess;
            this.proxy = proxy.Map(p => new WebProxy(p) as IWebProxy);
        }

        protected Task UpdatePackageState()
        {
            Console.WriteLine("Checking if aziot-edge and aziot-identity-service are installed.");
            return this.bootstrapper.UpdatePackageState();
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
            var csProperties = ParseConnectionString(this.iothubConnectionString);
            IotHubServiceClient serviceClient = new IotHubServiceClient(this.iothubConnectionString);

            Device device = await serviceClient.Devices.GetAsync(this.deviceId);
            if (device != null)
            {
                Console.WriteLine($"Device '{device.Id}' already registered on IoT hub '{csProperties["HostName"]}'");
                Console.WriteLine($"Clean up Existing device? {this.cleanUpExistingDeviceOnSuccess}");
                this.context = new DeviceContext(device, this.iothubConnectionString, serviceClient, this.cleanUpExistingDeviceOnSuccess);
            }
            else
            {
                // if dpsAttestion is enabled, do not create a device as the
                // ESD will register with DPS to create the device in IoT Hub
                if (this.dpsAttestation.HasValue)
                {
                    this.context = new DeviceContext(this.deviceId, this.iothubConnectionString, serviceClient, this.cleanUpExistingDeviceOnSuccess);
                }
                else
                {
                    await this.CreateEdgeDeviceIdentity(serviceClient);
                }
            }
        }

        protected Task ConfigureBootstrapper()
        {
            Console.WriteLine("Configuring bootstrapper.");
            DeviceProvisioningMethod method = this.dpsAttestation.Match(
                dps => { return new DeviceProvisioningMethod(dps); },
                () =>
                {
                    var csProperties = ParseConnectionString(this.context.IotHubConnectionString);
                    Device device = this.context.Device.Expect(() => new InvalidOperationException("Expected a valid device instance"));
                    string connectionString =
                        $"HostName={csProperties["HostName"]};" +
                        $"DeviceId={device.Id};" +
                        $"SharedAccessKey={device.Authentication.SymmetricKey.PrimaryKey}";

                    return new DeviceProvisioningMethod(connectionString);
                });

            Option<string> agentImage = Option.None<string>();
            if (this.initializeWithAgentArtifact)
            {
                agentImage = Option.Some<string>(this.EdgeAgentImage());
            }

            return this.bootstrapper.Configure(method, agentImage, this.hostname, this.parentHostname, this.deviceCaCert, this.deviceCaPk, this.deviceCaCerts, this.runtimeLogLevel);
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
            Console.WriteLine("Verifying if edge is connected to IoT Hub.");
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(600))) // Long timeout is needed because registry manager takes a while for the device identity to be usable
            {
                Exception savedException = null;

                try
                {
                    IotHubServiceClient serviceClient = new IotHubServiceClient(this.context.IotHubConnectionString);

                    while (!cts.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);

                        try
                        {
                            DirectMethodClientResponse result = await serviceClient.DirectMethods.InvokeAsync(
                                this.context.DeviceId,
                                "$edgeAgent",
                                new DirectMethodServiceRequest("ping"),
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

            Console.WriteLine($"Sending configuration to device '{this.context.DeviceId}' with modules:");
            foreach (string module in modules)
            {
                Console.WriteLine($"  {module}");
            }

            var config = JsonConvert.DeserializeObject<ConfigurationContent>(deployJson);

            var retryStrategy = new Incremental(15, RetryStrategy.DefaultRetryInterval, RetryStrategy.DefaultRetryIncrement);
            var retryPolicy = new RetryPolicy(new TransientNetworkErrorDetectionStrategy(), retryStrategy);
            return retryPolicy.ExecuteAsync(
                async () =>
            {
                Console.WriteLine("Attempting to apply configuration on device...");
                await this.context.ServiceClient.Configurations.ApplyConfigurationContentOnDeviceAsync(this.context.DeviceId, config);
            }, new CancellationTokenSource(TimeSpan.FromMinutes(10)).Token);
        }

        protected async Task VerifyDataOnIoTHub(string moduleId)
        {
            Console.WriteLine($"Verifying data on IoTHub from {moduleId}");

            // First Verify if module is already running.
            await this.bootstrapper.VerifyModuleIsRunning(moduleId);

            var builder = new EventHubsConnectionStringBuilder(this.eventhubCompatibleEndpointWithEntityPath)
            {
                TransportType = this.eventHubClientTransportType
            };

            Console.WriteLine($"Receiving events from device '{this.context.DeviceId}' on Event Hub '{builder.EntityPath}'");

            EventHubClient eventHubClient =
                EventHubClient.CreateFromConnectionString(builder.ToString());

            this.proxy.ForEach(p => eventHubClient.WebProxy = p);

            PartitionReceiver eventHubReceiver = eventHubClient.CreateReceiver(
                "$Default",
                EventHubPartitionKeyResolver.ResolveToPartition(
                    this.context.DeviceId,
                    (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                EventPosition.FromEnd());

            // TODO: [Improvement] should verify test results without using event hub, which introduce latency.
            var result = new TaskCompletionSource<bool>();
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20))) // This long timeout is needed in case event hub is slow to process messages
            {
                using (cts.Token.Register(() => result.TrySetCanceled()))
                {
                    eventHubReceiver.SetReceiveHandler(
                        new PartitionReceiveHandler(
                            eventData =>
                            {
                                eventData.SystemProperties.TryGetValue("iothub-connection-device-id", out object devId);
                                eventData.SystemProperties.TryGetValue("iothub-connection-module-id", out object modId);

                                if (devId != null && devId.ToString().Equals(this.context.DeviceId) &&
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

                    ClientTwin currentTwin = await this.context.ServiceClient.Twins.GetAsync(this.context.DeviceId, twinTest.ModuleId);

                    if (twinTest.Properties?.Desired?.Count > 0)
                    {
                        // Build Patch Object.
                        string patch = JsonConvert.SerializeObject(twinTest, Formatting.Indented);
                        var patchTwin = JsonConvert.DeserializeObject<ClientTwin>(patch);
                        await this.context.ServiceClient.Twins.UpdateAsync(this.context.DeviceId, twinTest.ModuleId, patchTwin, true, CancellationToken.None);
                    }

                    if (twinTest.Properties?.Reported?.Count > 0)
                    {
                        TimeSpan retryInterval = TimeSpan.FromSeconds(10);
                        bool IsValid(ClientTwinProperties currentTwinReportedProperty) => twinTest.Properties.Reported.All(p => currentTwinReportedProperty.ContainsKey(p.Key));

                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                        {
                            async Task<ClientTwinProperties> Func()
                            {
                                // Removing reSharper warning for CTS, Code Block will never exit before the delegate code completes because of using.
                                // ReSharper disable AccessToDisposedClosure
                                currentTwin = await this.context.ServiceClient.Twins.GetAsync(this.context.DeviceId, twinTest.ModuleId, cts.Token);
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

            return this.context.ServiceClient.Configurations.ApplyConfigurationContentOnDeviceAsync(this.context.DeviceId, config);
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
                return this.context.DeleteDevice();
            }

            return Task.CompletedTask;
        }

        async Task CreateEdgeDeviceIdentity(IotHubServiceClient serviceClient)
        {
            var device = new Device(this.deviceId)
            {
                Authentication = new AuthenticationMechanism() { Type = ClientAuthenticationType.Sas },
                Capabilities = new ClientCapabilities() { IsIotEdge = true }
            };

            await this.parentEdgeDevice.ForEachAsync(async p =>
            {
                var parentDevice = await serviceClient.Devices.GetAsync(p);
                device.ParentScopes.Add(parentDevice.Scope);
            });

            var csProperties = ParseConnectionString(this.iothubConnectionString);
            Console.WriteLine($"Registering device '{device.Id}' on IoT hub '{csProperties["HostName"]}'");

            var retryStrategy = new Incremental(15, RetryStrategy.DefaultRetryInterval, RetryStrategy.DefaultRetryIncrement);
            var retryPolicy = new RetryPolicy(new TransientNetworkErrorDetectionStrategy(), retryStrategy);
            await retryPolicy.ExecuteAsync(
                async () =>
            {
                Console.WriteLine("Attempting to create device identity...");
                device = await serviceClient.Devices.CreateAsync(device);
            }, new CancellationTokenSource(TimeSpan.FromMinutes(10)).Token);

            this.context = new DeviceContext(device, this.iothubConnectionString, serviceClient, true);
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

        static Dictionary<string, string> ParseConnectionString(string connectionString) =>
            connectionString.Split(';')
                .Select(part => part.Split(new[] { '=' }, 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
    }

    class TransientNetworkErrorDetectionStrategy : ITransientErrorDetectionStrategy
    {
        public bool IsTransient(Exception ex)
        {
            return ex is IotHubServiceException && ex.Message.Contains("The POST operation timed out");
        }
    }

    public class DeviceContext
    {
        public DeviceContext(string deviceId, string iothubConnectionString, IotHubServiceClient serviceClient, bool removeDevice)
        {
            this.DeviceId = deviceId;
            this.Device = Option.None<Device>();
            this.IotHubConnectionString = iothubConnectionString;
            this.ServiceClient = serviceClient;
            this.RemoveDevice = removeDevice;
        }

        public DeviceContext(Device device, string iothubConnectionString, IotHubServiceClient serviceClient, bool removeDevice)
        {
            this.DeviceId = device.Id;
            this.Device = Option.Some(device);
            this.IotHubConnectionString = iothubConnectionString;
            this.ServiceClient = serviceClient;
            this.RemoveDevice = removeDevice;
        }

        public Option<Device> Device { get; }

        public string DeviceId { get; }

        public string IotHubConnectionString { get; }

        public IotHubServiceClient ServiceClient { get; }

        public bool RemoveDevice { get; set; }

        public Task DeleteDevice()
        {
            if (this.RemoveDevice)
            {
                Console.WriteLine($"Trying to remove device from Registry. Device Id: {this.DeviceId}");
                return this.ServiceClient.Devices.DeleteAsync(this.DeviceId);
            }

            return Task.CompletedTask;
        }
    }
}
