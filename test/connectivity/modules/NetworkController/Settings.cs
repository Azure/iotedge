// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    class Settings
    {
        const string StartAfterPropertyName = "StartAfter";
        const string DockerUriPropertyName = "DockerUri";
        const string FrequencyPropertyName = "RunFrequencies";
        const string NetworkIdPropertyName = "NetworkId";
        const string NetworkControllerModePropertyName = "NetworkControllerMode";

        static readonly Lazy<Settings> Setting = new Lazy<Settings>(
            () =>
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/settings.json")
                    .AddEnvironmentVariables()
                    .Build();

                var result = new List<Frequency>();
                configuration.GetSection(FrequencyPropertyName).Bind(result);

                return new Settings(
                    configuration.GetValue<TimeSpan>(StartAfterPropertyName),
                    configuration.GetValue<string>(DockerUriPropertyName),
                    configuration.GetValue<string>(NetworkIdPropertyName),
                    result,
                    configuration.GetValue<NetworkControllerMode>(NetworkControllerModePropertyName));
            });

        Settings(
            TimeSpan startAfter,
            string dockerUri,
            string networkId,
            IList<Frequency> frequencies,
            NetworkControllerMode mode)
        {
            this.StartAfter = startAfter;
            this.Frequencies = frequencies;
            this.DockerUri = Preconditions.CheckNonWhiteSpace(dockerUri, nameof(dockerUri));
            this.NetworkId = Preconditions.CheckNonWhiteSpace(networkId, nameof(networkId));
            this.NetworkControllerMode = mode;
        }

        public static Settings Current => Setting.Value;

        public TimeSpan StartAfter { get; }

        public IList<Frequency> Frequencies { get; }

        public string DockerUri { get; }

        public string NetworkId { get; }

        public NetworkControllerMode NetworkControllerMode { get; }
    }

    class Frequency
    {
        public TimeSpan OfflineFrequency { get; set; }

        public TimeSpan OnlineFrequency { get; set; }

        public int RunsCount { get; set; }
    }
}
