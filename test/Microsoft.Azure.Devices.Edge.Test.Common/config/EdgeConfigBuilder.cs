// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeConfigBuilder
    {
        readonly string deviceId;
        readonly Dictionary<string, IModuleConfigBuilder> moduleBuilders;
        Option<(string address, string username, string password)> registry;

        public EdgeConfigBuilder(string deviceId)
        {
            this.deviceId = deviceId;
            this.moduleBuilders = new Dictionary<string, IModuleConfigBuilder>();
            this.registry = Option.None<(string, string, string)>();
        }

        public void AddRegistryCredentials(string address, string username, string password)
        {
            this.registry = Option.Some((address, username, password));
        }

        public IModuleConfigBuilder AddEdgeAgent(string image = null)
        {
            Option<string> imageOption = Option.Maybe(image);
            var builder = new AgentModuleConfigBuilder(imageOption);
            this.moduleBuilders.Add(builder.Name, builder);
            return builder;
        }

        public IModuleConfigBuilder AddEdgeHub(string image = null, bool optimizeForPerformance = true)
        {
            Option<string> imageOption = Option.Maybe(image);
            var builder = new HubModuleConfigBuilder(imageOption, optimizeForPerformance);
            this.moduleBuilders.Add(builder.Name, builder);
            return builder;
        }

        public IModuleConfigBuilder AddModule(string name, string image)
        {
            var builder = new ModuleConfigBuilder(name, image);
            this.moduleBuilders.Add(builder.Name, builder);
            return builder;
        }

        public EdgeConfiguration Build()
        {
            // Build all modules *except* edge agent
            List<ModuleConfiguration> modules = this.moduleBuilders
                .Where(b => b.Key != "$edgeAgent")
                .Select(b => b.Value.Build())
                .ToList();

            // Build edge agent
            modules.Insert(0, this.BuildEdgeAgent(modules));

            // Compose edge configuration
            var config = new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>()
            };

            var moduleImages = new List<string>();

            foreach (ModuleConfiguration module in modules)
            {
                moduleImages.Add(module.Image);

                if (module.DesiredProperties.Count != 0)
                {
                    config.ModulesContent[module.Name] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = module.DesiredProperties
                    };
                }
            }

            return new EdgeConfiguration(this.deviceId, moduleImages, config);
        }

        ModuleConfiguration BuildEdgeAgent(IEnumerable<ModuleConfiguration> configs)
        {
            if (!this.moduleBuilders.TryGetValue("$edgeAgent", out IModuleConfigBuilder agentBuilder))
            {
                agentBuilder = this.AddEdgeAgent();
            }

            // Settings boilerplate
            var settings = new Dictionary<string, object>()
            {
                ["minDockerVersion"] = "v1.25"
            };

            // Registry credentials under settings
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

            // Deployment info for edge agent
            var systemModules = new Dictionary<string, object>()
            {
                // We need to call agentBuilder.Build() *early* here because we
                // need the agent's own deployment information (e.g. image, env
                // vars). Even though we aren't finished building the agent
                // config yet, the agent builder has enough info at this
                // point to provide what we need. We'll call Build() again
                // later to get an updated module configuration that includes
                // deployment info for other modules, plus desired properties
                // for all modules.
                [ParseModuleName(agentBuilder.Name).name] = agentBuilder.Build().Deployment
            };

            // Deployment info for all other modules
            var modules = new Dictionary<string, object>();
            foreach (ModuleConfiguration config in configs)
            {
                (string name, bool system) = ParseModuleName(config.Name);
                IDictionary<string, object> coll = system ? systemModules : modules;
                coll[name] = config.Deployment;
            }

            // Compose edge agent's desired properties
            var desiredProperties = new Dictionary<string, object>
            {
                ["schemaVersion"] = "1.0",
                ["runtime"] = new Dictionary<string, object>
                {
                    ["type"] = "docker",
                    ["settings"] = settings
                },
                ["systemModules"] = systemModules
            };

            if (modules.Count != 0)
            {
                desiredProperties["modules"] = modules;
            }

            agentBuilder.WithDesiredProperties(desiredProperties);

            return agentBuilder.Build();
        }

        static (string name, bool system) ParseModuleName(string name) =>
            name.FirstOrDefault() == '$' ? (name.Substring(1), true) : (name, false);
    }
}
