// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public enum Protocol
    {
        Amqp,
        AmqpWs,
        Mqtt,
        MqttWs
    }

    public class EdgeConfiguration
    {
        readonly string deviceId;
        readonly IotHub iotHub;
        readonly IEnumerable<ModuleConfiguration> modules;
        readonly Option<(string address, string username, string password)> registry;

        public EdgeConfiguration(
            string deviceId,
            IEnumerable<ModuleConfiguration> modules,
            Option<(string address, string username, string password)> registry,
            IotHub iotHub)
        {
            this.deviceId = deviceId;
            this.iotHub = iotHub;
            this.modules = modules;
            this.registry = registry;
        }

        public Task DeployAsync(CancellationToken token)
        {
            var config = new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>()
            };

            foreach (ModuleConfiguration outer in this.modules)
            {
                if (outer.DesiredProperties.Count != 0)
                {
                    var desiredProperties = new Dictionary<string, object>(outer.DesiredProperties);
                    string name = outer.System ? $"${outer.Name}" : outer.Name;

                    if (name == "$edgeAgent")
                    {
                        // add registry credentials to $edgeAgent's settings
                        this.registry.ForEach(
                            r =>
                            {
                                var runtime = desiredProperties["runtime"] as IDictionary<string, object>;
                                var settings = runtime["settings"] as IDictionary<string, object>;
                                settings["registryCredentials"] = new
                                {
                                    reg1 = new
                                    {
                                        r.username,
                                        r.password,
                                        r.address
                                    }
                                };
                            });

                        // add each module's deployment info to $edgeAgent's desired properties
                        var systemModules = new Dictionary<string, object>();
                        var modules = new Dictionary<string, object>();

                        foreach (ModuleConfiguration inner in this.modules)
                        {
                            IDictionary<string, object> coll = inner.System ? systemModules : modules;
                            coll[inner.Name] = inner.Deployment;
                        }

                        if (systemModules.Count != 0)
                        {
                            desiredProperties["systemModules"] = systemModules;
                        }

                        if (modules.Count != 0)
                        {
                            desiredProperties["modules"] = modules;
                        }
                    }

                    config.ModulesContent.Add(
                        name,
                        new Dictionary<string, object>
                        {
                            ["properties.desired"] = desiredProperties
                        });
                }
            }

            return Profiler.Run(
               () => this.iotHub.DeployDeviceConfigurationAsync(this.deviceId, config, token),
               "Deployed edge configuration to device '{Device}' with modules ({Modules})",
               this.deviceId,
               string.Join(", ", this.ModuleNames));
        }

        IReadOnlyCollection<string> ModuleNames
        {
            get
            {
                List<string> list = this.modules.Select(m => m.Name).ToList();
                return new ReadOnlyCollection<string>(list);
            }
        }
    }
}
