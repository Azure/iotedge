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

        IReadOnlyCollection<string> _Modules
        {
            get
            {
                var list = new List<string>();
                _ForEachModule((name, module) =>
                {
                    list.Add(name);
                });
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
            _UpdateAgentDesiredProperties(desired =>
            {
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
            });
        }

        // Adds proxy information to each module in Edge Agent's desired properties. Call this
        // method after you've added all the modules that need proxy information.
        public void AddProxy(string url)
        {
            _ForEachModule((name, module) =>
            {
                JObject env = _GetOrAddObject("env", module);
                env.Add("https_proxy", JToken.FromObject(new
                {
                    value = url
                }));
                env.Add("UpstreamProtocol", JToken.FromObject(new
                {
                    value = UpstreamProtocolType.AmqpWs.ToString()
                }));
            });
        }

        public void AddEdgeHub(string image)
        {
            _UpdateAgentDesiredProperties(desired =>
            {
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
            });

            // { "modulesContent": { "$edgeHub": { ... } } }
            this.config.ModulesContent["$edgeHub"] = new Dictionary<string, object>
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
            _UpdateAgentDesiredProperties(desired =>
            {
                JObject modules = _GetOrAddObject("modules", desired);
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
            });
        }

        public Task DeployAsync(CancellationToken token)
        {
            string message = "Deploying edge configuration to device " +
                $"'{this.deviceId}' with modules ({string.Join(", ", this._Modules)})";

            return Profiler.Run(
                message,
                () => this.iotHub.DeployDeviceConfigurationAsync(this.deviceId, this.config, token)
            );
        }

        public JObject _GetOrAddObject(string name, JObject parent)
        {
            if (parent.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out JToken token))
            {
                return token.Value<JObject>();
            }

            parent.Add(name, new JObject());
            return parent.Get<JObject>(name);
        }

        void _ForEachModule(Action<string, JObject> action)
        {
            _UpdateAgentDesiredProperties(desired =>
            {
                foreach (var key in new[] { "systemModules", "modules" })
                {
                    if (desired.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken modules))
                    {
                        foreach (var module in modules.Value<JObject>())
                        {
                            action(module.Key, module.Value.Value<JObject>());
                        }
                    }
                }
            });
        }

        void _UpdateAgentDesiredProperties(Action<JObject> update)
        {
            JObject desired = JObject.FromObject(this.config.ModulesContent["$edgeAgent"]["properties.desired"]);
            update(desired);
            this.config.ModulesContent["$edgeAgent"]["properties.desired"] = desired;
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
