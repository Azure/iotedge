// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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

                string testResultCoordinatorUrl = string.IsNullOrWhiteSpace(configuration.GetValue<string>("testResultCoordinatorUrl"))
                    ? null
                    : configuration.GetValue<string>("testResultCoordinatorUrl");

                return new Settings(
                    configuration.GetValue("messageFrequency", TimeSpan.FromMilliseconds(20)),
                    configuration.GetValue<ulong>("messageSizeInBytes", 1024),
                    configuration.GetValue("transportType", TransportType.Amqp_Tcp_Only),
                    configuration.GetValue("outputName", "output1"),
                    configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)),
                    configuration.GetValue("testDuration", TimeSpan.Zero),
                    configuration.GetValue("trackingId", string.Empty),
                    Option.Maybe(testResultCoordinatorUrl),
                    configuration.GetValue<string>("IOTEDGE_MODULEID"));
            });

        Settings(
            TimeSpan messageFrequency,
            ulong messageSizeInBytes,
            TransportType transportType,
            string outputName,
            TimeSpan testStartDelay,
            TimeSpan testDuration,
            string trackingId,
            Option<string> testResultCoordinatorUrl,
            string moduleId)
        {
            Preconditions.CheckRange(messageFrequency.Ticks, 0);
            Preconditions.CheckRange(testStartDelay.Ticks, 0);
            Preconditions.CheckRange(testDuration.Ticks, 0);

            this.MessageSizeInBytes = Preconditions.CheckRange<ulong>(messageSizeInBytes, 1);
            this.OutputName = Preconditions.CheckNonWhiteSpace(outputName, nameof(outputName));

            this.MessageFrequency = messageFrequency;
            this.TestDuration = testDuration;
            this.TestStartDelay = testStartDelay;
            this.TrackingId = trackingId ?? string.Empty;
            this.TransportType = transportType;
            this.TestResultCoordinatorUrl = testResultCoordinatorUrl;
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
        }

        public static Settings Current => DefaultSettings.Value;

        public TimeSpan MessageFrequency { get; }

        public ulong MessageSizeInBytes { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public TransportType TransportType { get; }

        public string OutputName { get; }

        public TimeSpan TestStartDelay { get; }

        public TimeSpan TestDuration { get; }

        public string TrackingId { get; }

        public string ModuleId { get; }

        public Option<string> TestResultCoordinatorUrl { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>
            {
                { nameof(this.ModuleId), this.ModuleId },
                { nameof(this.MessageFrequency), this.MessageFrequency.ToString() },
                { nameof(this.MessageSizeInBytes), this.MessageSizeInBytes.ToString() },
                { nameof(this.OutputName), this.OutputName },
                { nameof(this.TestStartDelay), this.TestStartDelay.ToString() },
                { nameof(this.TestDuration), this.TestDuration.ToString() },
                { nameof(this.TrackingId), this.TrackingId },
                { nameof(this.TransportType), Enum.GetName(typeof(TransportType), this.TransportType) },
                { nameof(this.TestResultCoordinatorUrl), this.TestResultCoordinatorUrl.ToString() },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
