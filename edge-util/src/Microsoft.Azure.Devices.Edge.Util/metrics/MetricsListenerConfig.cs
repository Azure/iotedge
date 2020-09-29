// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using Microsoft.Extensions.Configuration;

    public class MetricsListenerConfig
    {
        const string DefaultHost = "*";
        const int DefaultPort = 9600;
        const string DefaultSuffix = "/metrics";

        public MetricsListenerConfig()
            : this(DefaultHost, DefaultPort, DefaultSuffix)
        {
        }

        public MetricsListenerConfig(string host, int port, string suffix)
        {
            this.Host = host ?? DefaultHost;
            this.Port = port > 0 ? port : DefaultPort;
            this.Suffix = suffix ?? DefaultSuffix;
        }

        public string Host { get; }

        public int Port { get; }

        public string Suffix { get; }

        public static MetricsListenerConfig Create(IConfiguration config)
        {
            string suffix = config.GetValue("suffix", DefaultSuffix);
            int port = config.GetValue("port", DefaultPort);
            string host = config.GetValue("host", DefaultHost);
            return new MetricsListenerConfig(host, port, suffix);
        }

        public override string ToString() => $"Host: {this.Host}, Port: {this.Port}, Suffix: {this.Suffix}";
    }
}
