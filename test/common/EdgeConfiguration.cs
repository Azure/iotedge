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

    public enum UpstreamProtocolType
    {
        Amqp,
        AmqpWs,
        Mqtt,
        MqttWs
    }

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

        public EdgeConfiguration(string deviceId, string agentImage, IotHub iotHub)
        {
            this.config = _GetBaseConfig(agentImage);
            this.iotHub = iotHub;
            this.deviceId = deviceId;
        }

        public void AddRegistryCredentials(string address, string username, string password)
        {
            ConfigurationContent config = this.config;

            // { "modulesContent": { "$edgeAgent": { "properties": { "desired": { "runtime": { "settings": {
            //   "registryCredentials": { ... }
            // } } } } } } }
            JObject desired = JObject.FromObject(config.ModulesContent["$edgeAgent"]["properties.desired"]);
            JObject settings = desired.Get<JObject>("runtime").Get<JObject>("settings");
            settings.Add("registryCredentials", JToken.FromObject(new
            {
                reg1 = new
                {
                    username = username,
                    password = password,
                    address = address
                }
            }));
            config.ModulesContent["$edgeAgent"]["properties.desired"] = desired;
        }

        public void AddProxy(string url)
        {
            JObject desired = JObject.FromObject(this.config.ModulesContent["$edgeAgent"]["properties.desired"]);
            if (desired.TryGetValue("systemModules", StringComparison.OrdinalIgnoreCase, out JToken systemModules))
            {
                foreach (var module in systemModules.Values<JObject>())
                {
                    JObject env;
                    if (module.TryGetValue("env", StringComparison.OrdinalIgnoreCase, out JToken token))
                    {
                        env = token.Value<JObject>();
                    }
                    else
                    {
                        module.Add("env", new JObject());
                        env = module.Get<JObject>("env");
                    }

                    env.Add("https_proxy", JToken.FromObject(new
                    {
                        value = url
                    }));
                    env.Add("UpstreamProtocol", JToken.FromObject(new
                    {
                        value = UpstreamProtocolType.AmqpWs.ToString()
                    }));
                }
            }
            if (desired.TryGetValue("modules", StringComparison.OrdinalIgnoreCase, out JToken modules))
            {
                foreach (var module in modules.Values<JObject>())
                {
                    JObject env;
                    if (module.TryGetValue("env", StringComparison.OrdinalIgnoreCase, out JToken token))
                    {
                        env = token.Value<JObject>();
                    }
                    else
                    {
                        module.Add("env", new JObject());
                        env = module.Get<JObject>("env");
                    }

                    env.Add("https_proxy", JToken.FromObject(new
                    {
                        value = url
                    }));
                    env.Add("UpstreamProtocol", JToken.FromObject(new
                    {
                        value = UpstreamProtocolType.AmqpWs.ToString()
                    }));
                }
            }

            this.config.ModulesContent["$edgeAgent"]["properties.desired"] = desired;
        }

        public void AddEdgeHub(string image)
        {
            ConfigurationContent config = this.config;

            // { "modulesContent": { "$edgeAgent": { "properties": { "desired": { "systemModules": {
            //   "edgeHub": { ... }
            // } } } } } }
            JObject desired = JObject.FromObject(config.ModulesContent["$edgeAgent"]["properties.desired"]);
            JObject systemModules = desired.Get<JObject>("systemModules");
            systemModules.Add("edgeHub", JToken.FromObject(new
            {
                type = "docker",
                status = "running",
                restartPolicy = "always",
                settings = new
                {
                    image = image,
                    createOptions = "{\"HostConfig\":{\"PortBindings\":{\"8883/tcp\":[{\"HostPort\":\"8883\"}],\"443/tcp\":[{\"HostPort\":\"443\"}],\"5671/tcp\":[{\"HostPort\":\"5671\"}]}}}"
                }
            }));
            config.ModulesContent["$edgeAgent"]["properties.desired"] = desired;

            // { "modulesContent": { "$edgeHub": { ... } } }
            config.ModulesContent["$edgeHub"] = new Dictionary<string, object>
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
        }

        public void AddTempSensor(string image)
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
            modules.Add("tempSensor", JToken.FromObject(new
            {
                type = "docker",
                status = "running",
                restartPolicy = "always",
                settings = new
                {
                    image = image
                }
            }));
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

        static ConfigurationContent _GetBaseConfig(string agentImage) => new ConfigurationContent
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
                                    image = agentImage
                                }
                            }
                        }
                    }
                }
            }
        };
   }
}
