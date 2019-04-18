// Copyright (c) Microsoft. All rights reserved.

namespace common
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json.Linq;

    public class EdgeConfiguration
    {
        ConfigurationContent config;
        string deviceId;
        IotHub iotHub;

        IReadOnlyCollection<string> Modules
        {
            get
            {
                var list = new List<string>();
                JObject desired = JObject.FromObject(this.config.ModulesContent["$edgeAgent"]["properties.desired"]);
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

        public EdgeConfiguration(string deviceId, IotHub iotHub)
        {
            this.config = _GetBaseConfig();
            this.iotHub = iotHub;
            this.deviceId = deviceId;
        }

        public void AddRegistryCredentials(string address, string username, string password)
        {
            // { "modulesContent": { "$edgeAgent": { "properties": { "desired": { "runtime": { "settings": {
            //   "registryCredentials": { ... }
            // } } } } } } }
            JObject desired = JObject.FromObject(config.ModulesContent["$edgeAgent"]["properties.desired"]);
            JObject settings = desired.Get<JObject>("runtime").Get<JObject>("settings");
            settings.Add("registryCredentials", JToken.FromObject(_GetBaseRegistryCredentials(
                address, username, password
            )));
            config.ModulesContent["$edgeAgent"]["properties.desired"] = desired;
        }

        public void AddEdgeHub()
        {
            ConfigurationContent config = this.config;

            // { "modulesContent": { "$edgeAgent": { "properties": { "desired": { "systemModules": {
            //   "edgeHub": { ... }
            // } } } } } }
            JObject desired = JObject.FromObject(config.ModulesContent["$edgeAgent"]["properties.desired"]);
            JObject systemModules = desired.Get<JObject>("systemModules");
            systemModules.Add("edgeHub", JToken.FromObject(_GetBaseEdgeHubSystemModules()));
            config.ModulesContent["$edgeAgent"]["properties.desired"] = desired;

            // { "modulesContent": { "$edgeHub": { ... } } }
            config.ModulesContent["$edgeHub"] = _GetBaseEdgeHubModulesContent();
        }

        public void AddTempSensor()
        {
            ConfigurationContent config = this.config;

            // { "modulesContent": { "$edgeAgent": { "properties": { "desired": { "modules": {
            //   "tempSensor": { ... }
            // } } } } } }
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
            modules.Add("tempSensor", JToken.FromObject(_GetBaseTempSensor()));
            config.ModulesContent["$edgeAgent"]["properties.desired"] = desired;
        }

        public Task DeployAsync(CancellationToken token)
        {
            string message = "Deploying edge configuration to device " +
                $"'{this.deviceId}' with modules ({string.Join(", ", this.Modules)})";

            return Profiler.Run(
                message,
                () => this.iotHub.DeployDeviceConfigurationAsync(this.deviceId, this.config, token)
            );
        }

        static ConfigurationContent _GetBaseConfig() => new ConfigurationContent
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

        static Object _GetBaseRegistryCredentials(
            string address,
            string username,
            string password
        ) => new
        {
            reg1 = new
            {
                username = username,
                password = password,
                address = address
            }
        };

        static Dictionary<string, object> _GetBaseEdgeHubModulesContent() => new Dictionary<string, object>
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

        static Object _GetBaseEdgeHubSystemModules() => new
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

        static Object _GetBaseTempSensor() => new
        {
            type = "docker",
            status = "running",
            restartPolicy = "always",
            settings = new
            {
                image = "mcr.microsoft.com/azureiotedge-simulated-temperature-sensor:1.0"
            }
        };
    }
}