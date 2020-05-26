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
        readonly List<Registry> registries;

        public EdgeConfigBuilder(string deviceId)
        {
            this.deviceId = deviceId;
            this.moduleBuilders = new Dictionary<string, IModuleConfigBuilder>();
            this.registries = new List<Registry>();
        }

        public void AddRegistry(Registry registry) =>
            this.registries.Add(registry);

        public void AddRegistries(IEnumerable<Registry> credentials)
        {
            foreach (Registry registry in credentials)
            {
                this.AddRegistry(registry);
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

        // By default, returns two configurations: one with just the system modules; the other with
        // the full configuration (if it contains more than just system modules). The first
        // configuration can be deployed in advance to ensure edgeHub's routes are ready before the
        // test modules start sending messages, to avoid dropped messages.
        // Note: Another option would be to define all possible routes at the beginning of the test
        // run, but then module names would need to be statically defined as well (currently they're
        // dynamic).
        // If stageSystemModules is false, returns one (full) configuration.
        public IEnumerable<EdgeConfiguration> Build(bool stageSystemModules = true)
        {
            // Edge agent is not optional; add if necessary
            if (!this.moduleBuilders.ContainsKey(ModuleName.EdgeAgent))
            {
                this.AddEdgeAgent();
            }

            ILookup<string, ModuleConfiguration> moduleConfigs = this.moduleBuilders
                .Where(b => b.Key != ModuleName.EdgeAgent) // delay building edge agent
                .Select(b => b.Value.Build())
                .ToLookup(m => m.IsSystemModule() ? "system" : "other");

            EdgeConfiguration BuildEdgeConfiguration(List<ModuleConfiguration> modules)
            {
                modules.Insert(0, this.BuildEdgeAgent(modules));
                return EdgeConfiguration.Create(this.deviceId, modules);
            }

            if (stageSystemModules)
            {
                // Return a configuration for $edgeHub and $edgeAgent
                yield return BuildEdgeConfiguration(moduleConfigs["system"].ToList());

                if (moduleConfigs.Contains("other"))
                {
                    // Return a configuration for all modules
                    yield return BuildEdgeConfiguration(moduleConfigs.SelectMany(m => m).ToList());
                }
            }
            else
            {
                yield return BuildEdgeConfiguration(moduleConfigs.SelectMany(m => m).ToList());
            }
        }

        ModuleConfiguration BuildEdgeAgent(IEnumerable<ModuleConfiguration> configs)
        {
            // caller guarantees that $edgeAgent exists in moduleBuilders
            IModuleConfigBuilder agentBuilder = this.moduleBuilders[ModuleName.EdgeAgent];

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
                    Registry registry = this.registries[i];
                    credentials.Add(
                        $"reg{i}",
                        new
                        {
                            registry.Username,
                            registry.Password,
                            registry.Address
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

        public IModuleConfigBuilder GetModule(string name)
        {
            return this.moduleBuilders[name];
        }

        public void RemoveModule(string name)
        {
            this.moduleBuilders.Remove(name);
        }

        static (string name, bool system) ParseModuleName(string name) =>
            name.FirstOrDefault() == '$' ? (name.Substring(1), true) : (name, false);
    }
}
