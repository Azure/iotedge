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
        readonly List<(string address, string username, string password)> registries;

        public EdgeConfigBuilder(string deviceId)
        {
            this.deviceId = deviceId;
            this.moduleBuilders = new Dictionary<string, IModuleConfigBuilder>();
            this.registries = new List<(string, string, string)>();
        }

        public void AddRegistryCredentials(string address, string username, string password) =>
            this.registries.Add((address, username, password));

        public void AddRegistryCredentials(IEnumerable<(string address, string username, string password)> credentials)
        {
            foreach ((string address, string username, string password) in credentials)
            {
                this.AddRegistryCredentials(address, username, password);
            }
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

        /* Will output edgeHub and edgeAgent configurations, then a full configuration at the end.
           This is done to assure routes are set up for the most recent $edgeHub deployment before the test modules start sending messages (i.e. assure messages won't get dropped).
           Another way to handle this is to define all possible routes at the very beginning of the test, but there is added complexity as module names are assigned dynamically. */
        public IEnumerable<EdgeConfiguration> BuildConfigurationStages()
        {
            // Build all modules *except* edge agent
            List<ModuleConfiguration> modules = this.moduleBuilders
                .Where(b => b.Key != "$edgeAgent")
                .Select(b => b.Value.Build())
                .ToList();

            // Build edge agent
            modules.Insert(0, this.BuildEdgeAgent(modules));

            // Find the $edgeAgent and $edgeHub match, then immediately compose and return a configuration
            var config = new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>()
            };

            var moduleNames = new List<string>();
            var moduleImages = new List<string>();

            // Return a configuration for $edgeHub and $edgeAgent
            foreach (ModuleConfiguration module in modules.Where(m => this.IsSystemModule(m)))
            {
                this.AddModuleToConfiguration(module, moduleNames, moduleImages, config);
            }

            Dictionary<string, IDictionary<string, object>> copyModulesContent = config.ModulesContent.ToDictionary(entry => entry.Key, entry => entry.Value);
            yield return new EdgeConfiguration(this.deviceId, new List<string>(moduleNames), new List<string>(moduleImages), new ConfigurationContent { ModulesContent = copyModulesContent });

            // Return a configuration for other modules
            foreach (ModuleConfiguration module in modules.Where(m => !this.IsSystemModule(m)))
            {
                this.AddModuleToConfiguration(module, moduleNames, moduleImages, config);
            }

            yield return new EdgeConfiguration(this.deviceId, moduleNames, moduleImages, config);
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
            if (this.registries.Count != 0)
            {
                var credentials = new Dictionary<string, object>();
                for (int i = 0; i < this.registries.Count; ++i)
                {
                    (string address, string username, string password) = this.registries[i];
                    credentials.Add(
                        $"reg{i}",
                        new
                        {
                            username,
                            password,
                            address
                        });
                }

                settings["registryCredentials"] = credentials;
            }

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

        bool IsSystemModule(ModuleConfiguration module)
        {
            return module.Name.Equals("$edgeHub") || module.Name.Equals("$edgeAgent");
        }

        void AddModuleToConfiguration(ModuleConfiguration module, List<string> moduleNames, List<string> moduleImages, ConfigurationContent config)
        {
            moduleNames.Add(module.Name);
            moduleImages.Add(module.Image);

            if (module.DesiredProperties.Count != 0)
            {
                config.ModulesContent[module.Name] = new Dictionary<string, object>
                {
                    ["properties.desired"] = module.DesiredProperties
                };
            }
        }

        public IModuleConfigBuilder GetModule(string name)
        {
            return this.moduleBuilders[name];
        }

        static (string name, bool system) ParseModuleName(string name) =>
            name.FirstOrDefault() == '$' ? (name.Substring(1), true) : (name, false);
    }
}
