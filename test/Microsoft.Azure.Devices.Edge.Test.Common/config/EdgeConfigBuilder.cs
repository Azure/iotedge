// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeConfigBuilder
    {
        readonly string deviceId;
        readonly IotHub iotHub;
        readonly Dictionary<(string name, bool system), IModuleConfigBuilder> moduleBuilders;
        Option<(string address, string username, string password)> registry;

        public EdgeConfigBuilder(string deviceId, IotHub iotHub)
        {
            this.deviceId = deviceId;
            this.iotHub = iotHub;
            this.moduleBuilders = new Dictionary<(string name, bool system), IModuleConfigBuilder>();
            this.registry = Option.None<(string, string, string)>();
        }

        public void AddRegistryCredentials(string address, string username, string password)
        {
            Preconditions.CheckNonWhiteSpace(address, nameof(address));
            Preconditions.CheckNonWhiteSpace(username, nameof(username));
            Preconditions.CheckNonWhiteSpace(password, nameof(password));

            this.registry = Option.Some((address, username, password));
        }

        public IModuleConfigBuilder AddEdgeAgent(string image = null)
        {
            // `image` cannot be empty. Builder will replace null with default.
            Option<string> imageOption = Option.Maybe(image);
            imageOption.ForEach(i => Preconditions.CheckNonWhiteSpace(i, nameof(i)));
            var builder = new AgentModuleConfigBuilder(imageOption);
            this.moduleBuilders.Add((builder.Name, builder.System), builder);
            return builder;
        }

        public IModuleConfigBuilder AddEdgeHub(string image = null)
        {
            // `image` cannot be empty. Builder will replace null with default.
            Option<string> imageOption = Option.Maybe(image);
            imageOption.ForEach(i => Preconditions.CheckNonWhiteSpace(i, nameof(i)));
            var builder = new HubModuleConfigBuilder(imageOption);
            this.moduleBuilders.Add((builder.Name, builder.System), builder);
            return builder;
        }

        public IModuleConfigBuilder AddModule(string name, string image)
        {
            Preconditions.CheckNonWhiteSpace(name, nameof(name));
            Preconditions.CheckNonWhiteSpace(image, nameof(image));
            var builder = new ModuleConfigBuilder(name, image);
            this.moduleBuilders.Add((builder.Name, builder.System), builder);
            return builder;
        }

        public EdgeConfiguration Build()
        {
            // Build all modules *except* edge agent
            List<ModuleConfiguration> modules = this.moduleBuilders
                .Where(b => !(b.Key.name == "edgeAgent" && b.Key.system))
                .Select(b => b.Value.Build())
                .ToList();

            // Build edge agent
            modules.Add(this.BuildEdgeAgent(modules));

            // Prepare edge configuration
            var config = new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>()
            };

            var moduleNames = new List<string>();

            foreach (ModuleConfiguration module in modules.Where(m => m.DesiredProperties.Count != 0))
            {
                string name = module.System ? $"${module.Name}" : module.Name;
                moduleNames.Add(name);

                config.ModulesContent[name] = new Dictionary<string, object>
                {
                    ["properties.desired"] = module.DesiredProperties
                };
            }

            return new EdgeConfiguration(this.deviceId, moduleNames, config, this.iotHub);
        }

        ModuleConfiguration BuildEdgeAgent(IEnumerable<ModuleConfiguration> configs)
        {
            if (!this.moduleBuilders.TryGetValue(("edgeAgent", true), out IModuleConfigBuilder agentBuilder))
            {
                agentBuilder = this.AddEdgeAgent();
            }

            // Add settings boilerplate
            var settings = new Dictionary<string, object>()
            {
                ["minDockerVersion"] = "v1.25"
            };

            // Add registry credentials under settings
            this.registry.ForEach(
                r =>
                {
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

            // Add schema version and runtime boilerplate
            var desiredProperties = new Dictionary<string, object>
            {
                ["schemaVersion"] = "1.0",
                ["runtime"] = new Dictionary<string, object>
                {
                    ["type"] = "docker",
                    ["settings"] = settings
                }
            };

            var systemModules = new Dictionary<string, object>()
            {
                // The agent builder has enough information to give its own
                // deployment. We'll call Build() again later to get an updated
                // module configuration that includes deployment info for other
                // modules, plus desired properties for all modules.
                [agentBuilder.Name] = agentBuilder.Build().Deployment
            };
            var modules = new Dictionary<string, object>();

            // Add other modules' deployment info
            foreach (ModuleConfiguration config in configs)
            {
                IDictionary<string, object> coll = config.System ? systemModules : modules;
                coll[config.Name] = config.Deployment;
            }

            desiredProperties["systemModules"] = systemModules;

            if (modules.Count != 0)
            {
                desiredProperties["modules"] = modules;
            }

            agentBuilder.WithDesiredProperties(desiredProperties);

            return agentBuilder.Build();
        }
    }
}
