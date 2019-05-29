// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json.Linq;

    public enum Protocol
    {
        Amqp,
        AmqpWs,
        Mqtt,
        MqttWs
    }

    public class EdgeConfiguration
    {
        readonly ConfigurationContent config;
        readonly string deviceId;
        readonly IotHub iotHub;

        public class Module
        {
            readonly string name;
            readonly EdgeConfiguration config;

            public Module(string name, EdgeConfiguration config)
            {
                this.name = name;
                this.config = config;
            }

            public Module WithEnvironment(IEnumerable<ValueTuple<string, string>> env)
            {
                this.config.UpdateModule(
                    this.name,
                    module =>
                    {
                        JObject envObj = GetOrAddObject("env", module);
                        foreach (ValueTuple<string, string> pair in env)
                        {
                            envObj.Add(pair.Item1, JToken.FromObject(new { value = pair.Item2 }));
                        }
                    });

                return this;
            }

            public Module WithProxy(Option<Uri> proxy, Protocol protocol)
            {
                proxy.ForEach(
                    p =>
                    {
                        string proxyProtocol;
                        switch (protocol)
                        {
                            case Protocol.Amqp:
                                proxyProtocol = Protocol.AmqpWs.ToString();
                                break;
                            case Protocol.Mqtt:
                                proxyProtocol = Protocol.MqttWs.ToString();
                                break;
                            case Protocol.AmqpWs:
                            case Protocol.MqttWs:
                                proxyProtocol = protocol.ToString();
                                break;
                            default:
                                throw new ArgumentException("Unknown protocol");
                        }

                        // If UpstreamProtocol was already set in this config to a compatible value,
                        // remove it so we can add the proxy-compatible value. If it's not compatible,
                        // throw an error. The caller will need to fix the conflict in their code.
                        Option<string> upstreamProtocol = this.GetEnvironmentVariable("UpstreamProtocol");
                        if (upstreamProtocol.Exists(u => u == protocol.ToString() || u == proxyProtocol))
                        {
                            this.RemoveEnvironmentVariable("UpstreamProtocol");
                        }
                        else
                        {
                            upstreamProtocol.ForEach(
                                u =>
                                {
                                    string message = $"Setting \"UpstreamProtocol\" to \"{proxyProtocol}\"" +
                                                     $"would overwrite incompatible value \"{u}\"";
                                    throw new ArgumentException(message);
                                });
                        }

                        this.WithEnvironment(
                            new[]
                            {
                                ("https_proxy", p.ToString()),
                                ("UpstreamProtocol", proxyProtocol)
                            });
                    });

                return this;
            }

            Option<string> GetEnvironmentVariable(string key)
            {
                Option<string> value = Option.None<string>();

                this.config.UpdateModule(
                    this.name,
                    module =>
                    {
                        if (module.TryGetValue("env", StringComparison.OrdinalIgnoreCase, out JToken envToken))
                        {
                            JObject envObj = envToken.Value<JObject>();
                            if (envObj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken valueToken))
                            {
                                value = Option.Maybe(valueToken.Value<JObject>().Value<string>("value"));
                            }
                        }
                    });

                return value;
            }

            void RemoveEnvironmentVariable(string key)
            {
                this.config.UpdateModule(
                    this.name,
                    module =>
                    {
                        if (module.TryGetValue("env", StringComparison.OrdinalIgnoreCase, out JToken envToken))
                        {
                            JObject envObj = envToken.Value<JObject>();
                            if (envObj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken valueToken))
                            {
                                valueToken.Parent.Remove();
                            }
                        }
                    });
            }
        }

        public EdgeConfiguration(string deviceId, IotHub iotHub)
        {
            this.config = GetBaseConfig();
            this.deviceId = deviceId;
            this.iotHub = iotHub;
        }

        IReadOnlyCollection<string> Modules
        {
            get
            {
                var list = new List<string>();
                this.ForEachModule((name, module) => { list.Add(name); });
                return new ReadOnlyCollection<string>(list);
            }
        }

        public void AddRegistryCredentials(string address, string username, string password)
        {
            this.UpdateAgentDesiredProperties(
                desired =>
                {
                    JObject settings = desired.Get<JObject>("runtime").Get<JObject>("settings");
                    settings.Add(
                        "registryCredentials",
                        JToken.FromObject(
                            new
                            {
                                reg1 = new
                                {
                                    username,
                                    password,
                                    address
                                }
                            }));
                });
        }

        public Module AddEdgeAgent(string image)
        {
            const string Name = "edgeAgent";

            this.UpdateAgentDesiredProperties(
                desired =>
                {
                    JObject systemModules = GetOrAddObject("systemModules", desired);
                    systemModules.Add(
                        Name,
                        JToken.FromObject(
                            new
                            {
                                type = "docker",
                                settings = new
                                {
                                    image
                                }
                            }));
                });

            return new Module(Name, this);
        }

        public Module AddEdgeHub(string image)
        {
            const string Name = "edgeHub";

            this.UpdateAgentDesiredProperties(
                desired =>
                {
                    JObject systemModules = GetOrAddObject("systemModules", desired);
                    systemModules.Add(
                        Name,
                        JToken.FromObject(
                            new
                            {
                                type = "docker",
                                status = "running",
                                restartPolicy = "always",
                                settings = new
                                {
                                    image,
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

            return new Module(Name, this);
        }

        public Module AddModule(string name, string image)
        {
            this.UpdateAgentDesiredProperties(
                desired =>
                {
                    JObject modules = GetOrAddObject("modules", desired);
                    modules.Add(
                        name,
                        JToken.FromObject(
                            new
                            {
                                type = "docker",
                                status = "running",
                                restartPolicy = "always",
                                settings = new
                                {
                                    image
                                }
                            }));
                });

            return new Module(name, this);
        }

        public Task DeployAsync(CancellationToken token)
        {
            return Profiler.Run(
                () => this.iotHub.DeployDeviceConfigurationAsync(this.deviceId, this.config, token),
                "Deployed edge configuration to device '{Device}' with modules ({Modules})",
                this.deviceId,
                string.Join(", ", this.Modules));
        }

        static JObject GetOrAddObject(string name, JObject parent)
        {
            if (parent.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out JToken token))
            {
                return token.Value<JObject>();
            }

            parent.Add(name, new JObject());
            return parent.Get<JObject>(name);
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
                        }
                    }
                }
            }
        };

        void UpdateModule(string name, Action<JObject> action) => this.ForEachModule(
            (moduleName, module) =>
            {
                if (moduleName == name)
                {
                    action(module);
                }
            });

        void ForEachModule(Action<string, JObject> action)
        {
            this.UpdateAgentDesiredProperties(
                desired =>
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

        void UpdateAgentDesiredProperties(Action<JObject> update)
        {
            JObject desired = JObject.FromObject(this.config.ModulesContent["$edgeAgent"]["properties.desired"]);
            update(desired);
            this.config.ModulesContent["$edgeAgent"]["properties.desired"] = desired;
        }
    }
}
