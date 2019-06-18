// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public abstract class BaseModuleConfigBuilder : IModuleConfigBuilder
    {
        readonly IDictionary<string, object> desiredProperties;
        readonly IDictionary<string, string> env;
        readonly IDictionary<string, object> deployment;
        readonly IDictionary<string, object> settings;

        public string Name { get; }

        protected BaseModuleConfigBuilder(string name, string image)
        {
            this.deployment = new Dictionary<string, object>()
            {
                ["type"] = "docker"
            };
            this.desiredProperties = new Dictionary<string, object>();
            this.env = new Dictionary<string, string>();
            this.Name = name;
            this.settings = new Dictionary<string, object>()
            {
                ["image"] = image
            };
        }

        protected void WithDeployment(IEnumerable<(string, string)> deployment)
        {
            foreach ((string key, string value) in deployment)
            {
                this.deployment[key] = value; // for duplicate keys, last save wins!
            }
        }

        protected void WithSettings(IEnumerable<(string, string)> settings)
        {
            foreach ((string key, string value) in settings)
            {
                this.settings[key] = value; // for duplicate keys, last save wins!
            }
        }

        public IModuleConfigBuilder WithEnvironment(IEnumerable<(string, string)> env)
        {
            foreach ((string key, string value) in env)
            {
                this.env[key] = value; // for duplicate keys, last save wins!
            }

            return this;
        }

        public IModuleConfigBuilder WithProxy(Option<Uri> proxy, Protocol protocol)
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

                    // If UpstreamProtocol was already set in this config to a non-compatible value,
                    // throw an error. The caller will need to fix the conflict in their code.
                    // Otherwise, replace it with the proxy-compatible value.
                    if (this.env.TryGetValue("UpstreamProtocol", out string u))
                    {
                        if (u != protocol.ToString() && u != proxyProtocol)
                        {
                            string message = $"Setting \"UpstreamProtocol\" to \"{proxyProtocol}\"" +
                                             $"would overwrite incompatible value \"{u}\"";
                            throw new ArgumentException(message);
                        }
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

        public IModuleConfigBuilder WithDesiredProperties(IDictionary<string, object> properties)
        {
            foreach ((string key, object value) in properties)
            {
                this.desiredProperties[key] = value; // for duplicate keys, last save wins!
            }

            return this;
        }

        // How is module information from this class composed into a
        // configuration JSON document? For a module, e.g. "myModule":
        //
        // modulesContent
        //   $edgeAgent
        //     properties.desired
        //       modules
        //         myModule         <== this.deployment
        //           settings       <== this.settings
        //           env            <== this.env
        //   myModule
        //     properties.desired   <== this.desiredProperties
        //
        // NOTE: This method is idempotent; it does not modify internal state
        // and can be called more than once.
        public ModuleConfiguration Build()
        {
            var deployment = new Dictionary<string, object>(this.deployment)
            {
                ["settings"] = this.settings
            };

            if (this.env.Count != 0)
            {
                deployment["env"] = this.env.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        value = kvp.Value
                    });
            }

            return new ModuleConfiguration(
                this.Name,
                new ReadOnlyDictionary<string, object>(deployment),
                new ReadOnlyDictionary<string, object>(this.desiredProperties));
        }
    }
}
