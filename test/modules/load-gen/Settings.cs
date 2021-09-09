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

    class Settings
    {
        internal static Settings Current = Create();

        Settings(
            TimeSpan messageFrequency,
            ulong messageSizeInBytes,
            TransportType transportType,
            string outputName,
            TimeSpan testStartDelay,
            TimeSpan testDuration,
            string trackingId,
            Option<string> testResultCoordinatorUrl,
            string moduleId,
            LoadGenSenderType senderType,
            Option<List<int>> priorities,
            Option<List<int>> ttls,
            Option<int> ttlThresholdSecs,
            Option<string> modelId)
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
            this.SenderType = senderType;
            this.Priorities = priorities;
            this.Ttls = ttls;
            this.TtlThresholdSecs = ttlThresholdSecs;
            this.ModelId = modelId;
        }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string testResultCoordinatorUrl = string.IsNullOrWhiteSpace(configuration.GetValue<string>("testResultCoordinatorUrl"))
                ? null
                : configuration.GetValue<string>("testResultCoordinatorUrl");

            List<int> priorities = string.IsNullOrWhiteSpace(configuration.GetValue<string>("priorities"))
                ? null
                : configuration.GetValue<string>("priorities").Split(';').Select(int.Parse).ToList();

            List<int> ttls = string.IsNullOrWhiteSpace(configuration.GetValue<string>("ttls"))
                ? null
                : configuration.GetValue<string>("ttls").Split(';').Select(int.Parse).ToList();

            int ttlThresholdValue = configuration.GetValue<int>("ttlThresholdSecs");

            if (LoadGenSenderType.PriorityMessageSender.Equals(configuration.GetValue<LoadGenSenderType>("senderType")))
            {
                if (ttls == null || ttlThresholdValue <= 0 || priorities == null)
                {
                    throw new ArgumentException("For PriorityMessageSender, ttls ttlThreshold, and priorities must all be set");
                }
                else if (ttls?.Count != 0 && ttls?.Count != priorities?.Count)
                {
                    throw new ArgumentException("TTL and priorities must have the same number of elements.");
                }
            }
            else
            {
                if (ttls != null || ttlThresholdValue != 0 || priorities != null)
                {
                    throw new ArgumentException("Tttls ttlThreshold, and priorities cannot be set unless PriorityMessageSender type is selected");
                }
            }

            Option<int> ttlThresholdSecs = ttlThresholdValue > 0 ? Option.Some(ttlThresholdValue) : Option.None<int>();

            return new Settings(
                configuration.GetValue("messageFrequency", TimeSpan.FromMilliseconds(20)),
                configuration.GetValue<ulong>("messageSizeInBytes", 1024),
                configuration.GetValue("transportType", TransportType.Amqp_Tcp_Only),
                configuration.GetValue("outputName", "output1"),
                configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)),
                configuration.GetValue("testDuration", TimeSpan.Zero),
                configuration.GetValue("trackingId", string.Empty),
                Option.Maybe(testResultCoordinatorUrl),
                configuration.GetValue<string>("IOTEDGE_MODULEID"),
                configuration.GetValue("senderType", LoadGenSenderType.DefaultSender),
                Option.Maybe(priorities),
                Option.Maybe(ttls),
                ttlThresholdSecs,
                Option.Maybe(configuration.GetValue<string>("modelId")));
        }

        public TimeSpan MessageFrequency { get; }

        public ulong MessageSizeInBytes { get; }

        public TransportType TransportType { get; }

        public string OutputName { get; }

        public TimeSpan TestStartDelay { get; }

        public TimeSpan TestDuration { get; }

        public string TrackingId { get; }

        public string ModuleId { get; }

        public Option<string> TestResultCoordinatorUrl { get; }

        public LoadGenSenderType SenderType { get; }

        public Option<List<int>> Priorities { get; }

        public Option<List<int>> Ttls { get; }

        public Option<int> TtlThresholdSecs { get; }

        public Option<string> ModelId { get; }

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
                { nameof(this.SenderType), this.SenderType.ToString() }
            };

            this.Priorities.ForEach(p => fields.Add(nameof(this.Priorities), p.ToString()));
            this.Ttls.ForEach(t => fields.Add(nameof(this.Ttls), t.ToString()));
            this.TtlThresholdSecs.ForEach(t => fields.Add(nameof(this.TtlThresholdSecs), t.ToString()));
            this.ModelId.ForEach(m => fields.Add(nameof(this.ModelId), m));

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
