// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public class EdgeConfiguration
    {
        readonly ConfigurationContent config;
        readonly string deviceId;
        readonly IEnumerable<string> moduleImages;

        public string[] ModuleNames { get; }

        public EdgeConfiguration(
            string deviceId,
            IEnumerable<string> moduleNames,
            IEnumerable<string> moduleImages,
            ConfigurationContent config)
        {
            this.config = config;
            this.deviceId = deviceId;
            this.moduleImages = moduleImages;
            this.ModuleNames = moduleNames
                .Select(id => id.StartsWith('$') ? id.Substring(1) : id)
                .ToArray();
        }

        public static EdgeConfiguration Create(string deviceId, List<ModuleConfiguration> modules)
        {
            var names = modules.Select(m => new StringBuilder(m.Name).ToString()).ToArray();
            var images = modules.Select(m => new StringBuilder(m.Image).ToString()).ToArray();
            var config = JsonConvert.SerializeObject(new ConfigurationContent
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
            }); // serialize/deserialize to make a copy

            return new EdgeConfiguration(
                deviceId,
                names,
                images,
                JsonConvert.DeserializeObject<ConfigurationContent>(config));
        }

        public Task DeployAsync(IotHub iotHub, CancellationToken token)
        {
            return Profiler.Run(
                () => iotHub.DeployDeviceConfigurationAsync(this.deviceId, this.config, token),
                "Deployed edge configuration to device with modules:\n    {Modules}",
                string.Join("\n    ", this.moduleImages));
        }

        public override string ToString() => JsonConvert.SerializeObject(this.config, Formatting.Indented);
    }
}
