// Copyright (c) Microsoft. All rights reserved.

namespace common
{
    using System.IO;

    public class DaemonConfiguration
    {
        const string ConfigYamlFile = @"C:\ProgramData\iotedge\config.yaml";

        YamlDocument config;

        public DaemonConfiguration()
        {
            string contents = File.ReadAllText(ConfigYamlFile);
            this.config = new YamlDocument(contents);
        }

        public void AddHttpsProxy(string proxyUrl)
        {
            this.config.ReplaceOrAdd("agent.env.https_proxy", proxyUrl);
            // TODO: When we allow the caller to specify an upstream protocol,
            //       we'll need to honor that if it's WebSocket-based, otherwise
            //       convert to an equivalent WebSocket-based protocol (e.g.,
            //       Mqtt --> MqttWs)
            this.config.ReplaceOrAdd("agent.env.UpstreamProtocol", "AmqpWs");
        }

        public void Update()
        {
            FileAttributes attr = 0;
            attr = File.GetAttributes(ConfigYamlFile);
            File.SetAttributes(ConfigYamlFile, attr & ~FileAttributes.ReadOnly);

            File.WriteAllText(ConfigYamlFile, this.config.ToString());

            if (attr != 0)
            {
                File.SetAttributes(ConfigYamlFile, attr);
            }
        }
    }
}
