// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;

    // TODO: remove
    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Settings
    {
        static readonly Lazy<Settings> DefaultSettings = new Lazy<Settings>(
            () =>
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/settings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                return new Settings(
                    configuration.GetValue<double>("jitterFactor", 0.5),
                    configuration.GetValue("twinUpdateFrequency", TimeSpan.FromMilliseconds(500)),
                    configuration.GetValue<TransportType>("transportType", TransportType.Amqp_Tcp_Only),
                    configuration.GetValue<string>("analyzerUrl", "http://analyzer:15000"),
                    configuration.GetValue<string>("serviceClientConnectionString", string.Empty));
            });

        Settings(
            double jitterFactor,
            TimeSpan twinUpdateFrequency,
            TransportType transportType,
            string analyzerUrl,
            string serviceClientConnectionString)
        {
            this.TwinUpdateFrequency = twinUpdateFrequency;
            this.TransportType = transportType;
            this.AnalyzerUrl = analyzerUrl;
            this.ServiceClientConnectionString = serviceClientConnectionString;
        }

        public static Settings Current => DefaultSettings.Value;

        public double JitterFactor { get; }
        public TimeSpan TwinUpdateFrequency { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public TransportType TransportType { get; }
        public string AnalyzerUrl { get; }
        public string ServiceClientConnectionString { get; }

        // TODO: change approach to not log connection string
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
