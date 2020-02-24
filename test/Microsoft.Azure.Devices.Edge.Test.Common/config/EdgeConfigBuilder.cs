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

        public void RemoveModule(string name)
        {
            this.moduleBuilders.Remove(name);
        }

        // Returns two configurations: one with just $edgeAgent and $edgeHub; the other with
        // everything. This is done to ensure edgeHub's routes are ready before the test modules
        // start sending messages, to avoid dropped messages. Another way to handle this is to
        // define all possible routes at the beginning of the test run, but there is added
        // complexity as module names are assigned dynamically.
        public IEnumerable<EdgeConfiguration> BuildConfigurationStages()
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

            // Return a configuration for $edgeHub and $edgeAgent
            List<ModuleConfiguration> modules = moduleConfigs["system"].ToList();
            modules.Insert(0, this.BuildEdgeAgent(modules));

            yield return new EdgeConfiguration(
                this.deviceId,
                modules.Select(m => m.Name).ToArray(),
                modules.Select(m => m.Image).ToArray(),
                new ConfigurationContent
                {
                    ModulesContent = modules
                        .Where(m => m.DesiredProperties.Count != 0)
                        .Select(m => new KeyValuePair<string, IDictionary<string, object>>(
                            m.Name,
                            new Dictionary<string, object>
                            {
                                ["properties.desired"] = m.DesiredProperties
                            }))
                        .ToDictionary(x => x.Key, x => x.Value)
                });

            if (moduleConfigs.Contains("other"))
            {
                // Return a configuration for all modules
                modules = moduleConfigs.SelectMany(m => m).ToList();
                modules.Insert(0, this.BuildEdgeAgent(modules));

                yield return new EdgeConfiguration(
                    this.deviceId,
                    modules.Select(m => m.Name).ToArray(),
                    modules.Select(m => m.Image).ToArray(),
                    new ConfigurationContent
                    {
                        ModulesContent = modules
                            .Where(m => m.DesiredProperties.Count != 0)
                            .Select(m => new KeyValuePair<string, IDictionary<string, object>>(
                                m.Name,
                                new Dictionary<string, object>
                                {
                                    ["properties.desired"] = m.DesiredProperties
                                }))
                            .ToDictionary(x => x.Key, x => x.Value)
                    });
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

        public IModuleConfigBuilder GetModule(string name)
        {
            return this.moduleBuilders[name];
        }

        static (string name, bool system) ParseModuleName(string name) =>
            name.FirstOrDefault() == '$' ? (name.Substring(1), true) : (name, false);
    }
}
