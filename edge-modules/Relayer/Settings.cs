// Copyright (c) Microsoft. All rights reserved.
namespace Relayer
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;

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
                    configuration.GetValue("transportType", TransportType.Amqp_Tcp_Only),
                    configuration.GetValue("outputName", "output1"));
            });

        Settings(
            TransportType transportType,
            string outputName)
        {
            this.TransportType = Preconditions.CheckNotNull(transportType);
            this.OutputName = Preconditions.CheckNonWhiteSpace(outputName, nameof(outputName));
        }

        public static Settings Current => DefaultSettings.Value;

        [JsonConverter(typeof(StringEnumConverter))]
        public TransportType TransportType { get; }

        public string OutputName { get; }

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}
