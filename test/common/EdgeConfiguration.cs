// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Edge.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace common
{
    public class EdgeConfiguration
    {
        string deviceId;
        string hubConnectionString;
        ConfigurationContent config;

        public EdgeConfiguration(string deviceId, string hubConnectionString)
        {
            this.deviceId = deviceId;
            this.hubConnectionString = hubConnectionString;
            this.config = GetBaseConfig();
        }

        public void AddEdgeHub()
        {
            ConfigurationContent config = this.config;

            // { "modulesContent": { "$edgeAgent": { "properties.desired": { "systemModules": { "edgeHub": { ... } } } } } }
            JObject desired = JObject.FromObject(config.ModulesContent["$edgeAgent"]["properties.desired"]);
            JObject systemModules = desired.Get<JObject>("systemModules");
            systemModules.Add("edgeHub", JToken.FromObject(GetBaseEdgeHubSystemModules()));
            config.ModulesContent["$edgeAgent"]["properties.desired"] = desired;

            // { "modulesContent": { "$edgeHub": { ... } } }
            config.ModulesContent["$edgeHub"] = GetBaseEdgeHubModulesContent();
        }

        public void AddTempSensor()
        {
            ConfigurationContent config = this.config;

            // { "modulesContent": { "$edgeAgent": { "properties.desired": { "modules": { "tempSensor": { ... } } } } } }
            JObject desired = JObject.FromObject(config.ModulesContent["$edgeAgent"]["properties.desired"]);
            JObject modules;
            if (desired.TryGetValue("modules", StringComparison.OrdinalIgnoreCase, out JToken token))
            {
                modules = token.Value<JObject>();
            }
            else
            {
                desired.Add("modules", new JObject());
                modules = desired.Get<JObject>("modules");
            }
            modules.Add("tempSensor", JToken.FromObject(GetBaseTempSensor()));
            config.ModulesContent["$edgeAgent"]["properties.desired"] = desired;
        }

        public async Task DeployAsync()
        {
            var settings = new HttpTransportSettings();
            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.hubConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(builder.ToString(), settings);
            await rm.ApplyConfigurationContentOnDeviceAsync(this.deviceId, this.config);

            IReadOnlyCollection<string> modules = GetConfigModuleList(this.config);
            Console.WriteLine($"Edge configuration was deployed to device '{this.deviceId}' with modules:");
            foreach (string module in modules)
            {
                Console.WriteLine($"  {module}");
            }
        }

        static ConfigurationContent GetBaseConfig() => new ConfigurationContent
        {
            ModulesContent = new Dictionary<string, IDictionary<string, object>>
            {
                ["$edgeAgent"] = new Dictionary<string, object>
                {
                    ["properties.desired"] = new
                    {
                        schemaVersion = "1.0",
                        runtime = new
                        {
                            type = "docker",
                            settings = new
                            {
                                minDockerVersion = "v1.25"
                            }
                        },
                        systemModules = new
                        {
                            edgeAgent = new
                            {
                                type = "docker",
                                settings = new
                                {
                                    image = "mcr.microsoft.com/azureiotedge-agent:1.0"
                                }
                            }
                        }
                    }
                }
            }
        };

        static Dictionary<string, object> GetBaseEdgeHubModulesContent() => new Dictionary<string, object>
        {
            ["properties.desired"] = new
            {
                schemaVersion = "1.0",
                routes = new Dictionary<string, string>
                {
                    ["route1"] = "from /* INTO $upstream",
                },
                storeAndForwardConfiguration = new
                {
                    timeToLiveSecs = 7200
                }
            }
        };

        static Object GetBaseEdgeHubSystemModules() => new
        {
            type = "docker",
            status = "running",
            restartPolicy = "always",
            settings = new
            {
                image = "mcr.microsoft.com/azureiotedge-hub:1.0",
                createOptions = "{\"HostConfig\":{\"PortBindings\":{\"8883/tcp\":[{\"HostPort\":\"8883\"}],\"443/tcp\":[{\"HostPort\":\"443\"}],\"5671/tcp\":[{\"HostPort\":\"5671\"}]}}}"
            }
        };

        static Object GetBaseTempSensor() => new
        {
            type = "docker",
            status = "running",
            restartPolicy = "always",
            settings = new
            {
                image = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0"
            }
        };

        static IReadOnlyCollection<string> GetConfigModuleList(ConfigurationContent config)
        {
            var list = new List<string>();
            JObject desired = JObject.FromObject(config.ModulesContent["$edgeAgent"]["properties.desired"]);
            if (desired.TryGetValue("systemModules", StringComparison.OrdinalIgnoreCase, out JToken systemModules))
            {
                foreach (var module in systemModules.Value<JObject>())
                {
                    list.Add(module.Key);
                }
            }
            if (desired.TryGetValue("modules", StringComparison.OrdinalIgnoreCase, out JToken modules))
            {
                foreach (var module in modules.Value<JObject>())
                {
                    list.Add(module.Key);
                }
            }
            return new ReadOnlyCollection<string>(list);
        }
    }
}