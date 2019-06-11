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
            this.moduleBuilders.Add(builder.Name, builder);
            return builder;
        }

        public IModuleConfigBuilder AddEdgeHub(string image = null)
        {
            // `image` cannot be empty. Builder will replace null with default.
            Option<string> imageOption = Option.Maybe(image);
            imageOption.ForEach(i => Preconditions.CheckNonWhiteSpace(i, nameof(i)));
            var builder = new HubModuleConfigBuilder(imageOption);
            this.moduleBuilders.Add(builder.Name, builder);
            return builder;
        }

        public IModuleConfigBuilder AddModule(string name, string image)
        {
            Preconditions.CheckNonWhiteSpace(name, nameof(name));
            Preconditions.CheckNonWhiteSpace(image, nameof(image));
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

            // Prepare edge configuration
            var config = new ConfigurationContent
            {
                ModulesContent = new Dictionary<string, IDictionary<string, object>>()
            };

            var moduleNames = new List<string>();

            foreach (ModuleConfiguration module in modules)
            {
                moduleNames.Add(module.Name);

                if (module.DesiredProperties.Count != 0)
                {
                    config.ModulesContent[module.Name] = new Dictionary<string, object>
                    {
                        ["properties.desired"] = module.DesiredProperties
                    };
                }
            }

            return new EdgeConfiguration(this.deviceId, moduleNames, config);
        }

        ModuleConfiguration BuildEdgeAgent(IEnumerable<ModuleConfiguration> configs)
        {
            if (!this.moduleBuilders.TryGetValue("$edgeAgent", out IModuleConfigBuilder agentBuilder))
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
            var modules = new Dictionary<string, object>();

            // Add other modules' deployment info
            foreach (ModuleConfiguration config in configs)
            {
                var parsed = ParseModuleName(config.Name);
                IDictionary<string, object> coll = parsed.system ? systemModules : modules;
                coll[parsed.name] = config.Deployment;
            }

            desiredProperties["systemModules"] = systemModules;

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
