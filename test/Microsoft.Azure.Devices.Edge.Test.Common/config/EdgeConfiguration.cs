// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public enum Protocol
    {
        Amqp,
        AmqpWs,
        Mqtt,
        MqttWs
    }

    public class EdgeConfiguration
    {
        readonly ConfigurationContent config;
        readonly string deviceId;
        readonly IotHub iotHub;
        readonly IEnumerable<string> moduleNames;

        public EdgeConfiguration(
            string deviceId,
            IEnumerable<string> moduleNames,
            ConfigurationContent config,
            IotHub iotHub)
        {
            this.config = config;
            this.deviceId = deviceId;
            this.iotHub = iotHub;
            this.moduleNames = moduleNames;
        }

        public Task DeployAsync(CancellationToken token)
        {
            return Profiler.Run(
                () => this.iotHub.DeployDeviceConfigurationAsync(this.deviceId, this.config, token),
                "Deployed edge configuration to device '{Device}' with modules ({Modules})",
                this.deviceId,
                string.Join(", ", this.moduleNames));
        }

        public override string ToString() => JsonConvert.SerializeObject(this.config, Formatting.Indented);
    }
}
