// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public class EdgeConfiguration
    {
        readonly ConfigurationContent config;
        readonly string deviceId;
        readonly IEnumerable<string> moduleImages;

        public EdgeConfiguration(
            string deviceId,
            IEnumerable<string> moduleImages,
            ConfigurationContent config)
        {
            this.config = config;
            this.deviceId = deviceId;
            this.moduleImages = moduleImages;
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
