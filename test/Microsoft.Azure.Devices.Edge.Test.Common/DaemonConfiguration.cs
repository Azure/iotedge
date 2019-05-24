// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.IO;
    using Serilog;

    public class DaemonConfiguration
    {
        const string ConfigYamlFile = @"C:\ProgramData\iotedge\config.yaml";

        YamlDocument config;

        public DaemonConfiguration()
        {
            string contents = File.ReadAllText(ConfigYamlFile);
            this.config = new YamlDocument(contents);
        }

        public void AddHttpsProxy(Uri proxy)
        {
            this.config.ReplaceOrAdd("agent.env.https_proxy", proxy.ToString());
            // TODO: When we allow the caller to specify an upstream protocol,
            //       we'll need to honor that if it's WebSocket-based, otherwise
            //       convert to an equivalent WebSocket-based protocol (e.g.,
            //       Mqtt --> MqttWs)
            this.config.ReplaceOrAdd("agent.env.UpstreamProtocol", "AmqpWs");
        }

        public void Update()
        {
            var attr = File.GetAttributes(ConfigYamlFile);
            File.SetAttributes(ConfigYamlFile, attr & ~FileAttributes.ReadOnly);

            File.WriteAllText(ConfigYamlFile, this.config.ToString());

            if (attr != 0)
            {
                File.SetAttributes(ConfigYamlFile, attr);
            }

            Log.Information("Updated daemon configuration file '{ConfigFile}'", ConfigYamlFile);
        }
    }
}
