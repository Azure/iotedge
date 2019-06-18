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
        readonly IEnumerable<string> moduleNames;

        public EdgeConfiguration(
            string deviceId,
            IEnumerable<string> moduleNames,
            ConfigurationContent config)
        {
            this.config = config;
            this.deviceId = deviceId;
            this.moduleNames = moduleNames;
        }

        public Task DeployAsync(IotHub iotHub, CancellationToken token)
        {
            return Profiler.Run(
                () => iotHub.DeployDeviceConfigurationAsync(this.deviceId, this.config, token),
                "Deployed edge configuration to device with modules ({Modules})",
                string.Join(", ", this.moduleNames));
        }

        public override string ToString() => JsonConvert.SerializeObject(this.config, Formatting.Indented);
    }
}
