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
        const string TestResultCoordinatorEndpointPropertyName = "TestResultCoordinatorEndpoint";
        const string TrackingIdPropertyName = "TrackingId";
        const string ModuleIdPropertyName = "IOTEDGE_MODULEID";
        const string IotHubHostnamePropertyName = "IOTEDGE_IOTHUBHOSTNAME";

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
                    configuration.GetValue<NetworkControllerMode>(NetworkControllerModePropertyName),
                    configuration.GetValue<Uri>(TestResultCoordinatorEndpointPropertyName),
                    configuration.GetValue<string>(TrackingIdPropertyName),
                    configuration.GetValue<string>(ModuleIdPropertyName),
                    configuration.GetValue<string>(IotHubHostnamePropertyName));
            });

        Settings(
            TimeSpan startAfter,
            string dockerUri,
            string networkId,
            IList<Frequency> frequencies,
            NetworkControllerMode mode,
            Uri testResultCoordinatorEndpoint,
            string trackingId,
            string moduleId,
            string iothubHostname)
        {
            this.StartAfter = startAfter;
            this.Frequencies = frequencies;
            this.DockerUri = Preconditions.CheckNonWhiteSpace(dockerUri, nameof(dockerUri));
            this.NetworkId = Preconditions.CheckNonWhiteSpace(networkId, nameof(networkId));
            this.TestResultCoordinatorEndpoint = Preconditions.CheckNotNull(testResultCoordinatorEndpoint, nameof(testResultCoordinatorEndpoint));
            this.NetworkControllerMode = mode;
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.IotHubHostname = Preconditions.CheckNonWhiteSpace(iothubHostname, nameof(iothubHostname));
        }

        public static Settings Current => Setting.Value;

        public TimeSpan StartAfter { get; }

        public IList<Frequency> Frequencies { get; }

        public string DockerUri { get; }

        public string NetworkId { get; }

        public Uri TestResultCoordinatorEndpoint { get; }

        public NetworkControllerMode NetworkControllerMode { get; }

        public string TrackingId { get; }

        public string ModuleId { get; }

        public string IotHubHostname { get; }
    }

    class Frequency
    {
        public TimeSpan OfflineFrequency { get; set; }

        public TimeSpan OnlineFrequency { get; set; }

        public int RunsCount { get; set; }
    }
}
