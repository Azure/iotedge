// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Collections.Generic;
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
                    configuration.GetValue("messageFrequency", TimeSpan.FromMilliseconds(20)),
                    configuration.GetValue<ulong>("messageSizeInBytes", 1024),
                    configuration.GetValue("transportType", TransportType.Amqp_Tcp_Only),
                    configuration.GetValue("outputName", "output1"),
                    configuration.GetValue("startDelay", TimeSpan.FromSeconds(2)));
            });

        Settings(
            TimeSpan messageFrequency,
            ulong messageSizeInBytes,
            TransportType transportType,
            string outputName,
            TimeSpan startDelay)
        {
            this.MessageFrequency = Preconditions.CheckNotNull(messageFrequency);
            this.MessageSizeInBytes = Preconditions.CheckNotNull(messageSizeInBytes);
            this.TransportType = Preconditions.CheckNotNull(transportType);
            this.OutputName = Preconditions.CheckNonWhiteSpace(outputName, nameof(outputName));
            this.StartDelay = Preconditions.CheckNotNull(startDelay);
        }

        public static Settings Current => DefaultSettings.Value;

        public TimeSpan MessageFrequency { get; }

        public ulong MessageSizeInBytes { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public TransportType TransportType { get; }

        public string OutputName { get; }

        public TimeSpan StartDelay { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            Dictionary<string, string> fields = new Dictionary<string, string>();
            fields.Add(nameof(this.MessageFrequency), this.MessageFrequency.ToString());
            fields.Add(nameof(this.MessageSizeInBytes), this.MessageSizeInBytes.ToString());
            fields.Add(nameof(this.TransportType), Enum.GetName(typeof(TransportType), this.TransportType));
            fields.Add(nameof(this.OutputName), this.OutputName);
            fields.Add(nameof(this.StartDelay), this.StartDelay.ToString());
            return JsonConvert.SerializeObject(fields, Formatting.Indented);
        }
    }
}
