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
        IDictionary<string, object> DesiredProperties { get; }

        IDictionary<string, string> Env { get; }

        protected IDictionary<string, object> Deployment { get; }

        protected IDictionary<string, object> Settings { get; }

        public string Name { get; }

        protected BaseModuleConfigBuilder(string name, string image)
        {
            this.Deployment = new Dictionary<string, object>()
            {
                ["type"] = "docker"
            };
            this.DesiredProperties = new Dictionary<string, object>();
            this.Env = new Dictionary<string, string>();
            this.Name = Preconditions.CheckNonWhiteSpace(name, nameof(name));
            this.Settings = new Dictionary<string, object>()
            {
                ["image"] = Preconditions.CheckNonWhiteSpace(image, nameof(image))
            };
        }

        public IModuleConfigBuilder WithEnvironment(IEnumerable<(string, string)> env)
        {
            foreach ((string key, string value) in env)
            {
                this.Env[key] = value; // for duplicate keys, last save wins!
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
                    if (this.Env.TryGetValue("UpstreamProtocol", out string u))
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
                this.DesiredProperties[key] = value; // for duplicate keys, last save wins!
            }

            return this;
        }

        public ModuleConfiguration Build()
        {
            // Compose the hierarchy
            var deployment = new Dictionary<string, object>(this.Deployment)
            {
                ["settings"] = this.Settings
            };

            if (this.Env.Count != 0)
            {
                deployment["env"] = this.Env.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        value = kvp.Value
                    });
            }

            return new ModuleConfiguration(
                this.Name,
                new ReadOnlyDictionary<string, object>(deployment),
                new ReadOnlyDictionary<string, object>(this.DesiredProperties));
        }
    }
}
