// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using System.Linq;
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
