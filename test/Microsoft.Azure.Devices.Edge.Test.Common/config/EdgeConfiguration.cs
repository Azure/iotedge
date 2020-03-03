// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class EdgeConfiguration
    {
        readonly ConfigurationContent config;
        readonly string deviceId;
        readonly object expectedConfig;
        readonly IEnumerable<string> moduleImages;

        public string[] ModuleNames { get; }

        public EdgeConfiguration(
            string deviceId,
            IEnumerable<string> moduleNames,
            IEnumerable<string> moduleImages,
            ConfigurationContent config,
            object expectedConfig)
        {
            this.config = config;
            this.deviceId = deviceId;
            this.expectedConfig = expectedConfig;
            this.moduleImages = moduleImages;
            this.ModuleNames = moduleNames
                .Select(id => id.StartsWith('$') ? id.Substring(1) : id)
                .ToArray();
        }

        public static EdgeConfiguration Create(string deviceId, IEnumerable<ModuleConfiguration> moduleConfigs)
        {
            ModuleConfiguration[] modules = moduleConfigs.ToArray();

            var names = modules.Select(m => m.Name).ToArray();
            var images = modules.Select(m => m.Image).ToArray();
            var config = new ConfigurationContent
            {
                ModulesContent = modules
                    .Where(m => m.DesiredProperties.Count != 0)
                    .ToDictionary(
                        m => m.Name,
                        m => (IDictionary<string, object>)new Dictionary<string, object>
                        {
                            ["properties.desired"] = m.DesiredProperties
                        })
            };

            // Make a copy
            config = JsonConvert.DeserializeObject<ConfigurationContent>(JsonConvert.SerializeObject(config));

            // Build the object we'll use later to verify the deployment
            var edgeAgent = modules.Where(m => m.Name == ModuleName.EdgeAgent).FirstOrDefault()
                ?? new ModuleConfiguration();
            var desired = JObject
                .FromObject(edgeAgent.DesiredProperties);

            var reported = new Dictionary<string, object>
            {
                ["systemModules"] = desired
                            .Value<JObject>("systemModules")
                            .Children<JProperty>()
                            .ToDictionary(
                                p => p.Name,
                                p => CreateExpectedModuleConfig((JObject)p.Value))
            };

            if (desired.ContainsKey("modules"))
            {
                reported["modules"] = desired
                    .Value<JObject>("modules")
                    .Children<JProperty>()
                    .ToDictionary(
                        p => p.Name,
                        p => CreateExpectedModuleConfig((JObject)p.Value));
            }

            var expected = new
            {
                properties = new
                {
                    reported = reported
                }
            };

            return new EdgeConfiguration(deviceId, names, images, config, expected);
        }

        static object CreateExpectedModuleConfig(JObject source)
        {
            var image = source.SelectToken($"settings.image"); // not optional
            var createOptions = source.SelectToken($"settings.createOptions") ?? new JObject();
            var env = source.SelectToken($"env") ?? new JObject();

            var module = new
            {
                settings = new
                {
                    image = image.Value<string>(),
                    createOptions = createOptions.ToString()
                },
                env = env.ToObject<IDictionary<string, object>>()
            };

            return module;
        }

        public Task DeployAsync(IotHub iotHub, CancellationToken token)
        {
            return Profiler.Run(
                () => iotHub.DeployDeviceConfigurationAsync(this.deviceId, this.config, token),
                "Deployed edge configuration to device with modules:\n    {Modules}",
                string.Join("\n    ", this.moduleImages));
        }

        public Task VerifyAsync(IotHub iotHub, CancellationToken token)
        {
            EdgeAgent agent = new EdgeAgent(this.deviceId, iotHub);
            return agent.WaitForReportedConfigurationAsync(this.expectedConfig, token);
        }

        public override string ToString() => JsonConvert.SerializeObject(this.config, Formatting.Indented);
    }
}
