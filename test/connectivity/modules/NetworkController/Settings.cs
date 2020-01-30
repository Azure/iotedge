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
        internal static Settings Current = Create();
        const string StartAfterPropertyName = "StartAfter";
        const string DockerUriPropertyName = "DockerUri";
        const string FrequencyPropertyName = "RunFrequencies";
        const string NetworkIdPropertyName = "NetworkId";
        const string TestResultCoordinatorEndpointPropertyName = "TestResultCoordinatorEndpoint";
        const string TrackingIdPropertyName = "TrackingId";
        const string ModuleIdPropertyName = "IOTEDGE_MODULEID";
        const string IotHubHostnamePropertyName = "IOTEDGE_IOTHUBHOSTNAME";
        const string NetworkControllerRunProfilePropertyName = "NetworkControllerRunProfile";
        const string DefaultProfilesPropertyName = "DefaultProfiles";

        Settings(
            TimeSpan startAfter,
            string dockerUri,
            string networkId,
            IList<Frequency> frequencies,
            string networkRunProfile,
            IDictionary<string, NetworkProfile> profileSettings,
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
            Preconditions.CheckNonWhiteSpace(networkRunProfile, nameof(networkRunProfile));
            Preconditions.CheckNotNull(profileSettings, nameof(profileSettings));
            Preconditions.CheckArgument(profileSettings.ContainsKey(networkRunProfile));
            this.NetworkRunProfile = profileSettings[networkRunProfile];
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.IotHubHostname = Preconditions.CheckNonWhiteSpace(iothubHostname, nameof(iothubHostname));
        }

        public TimeSpan StartAfter { get; }

        public IList<Frequency> Frequencies { get; }

        public string DockerUri { get; }

        public string NetworkId { get; }

        public Uri TestResultCoordinatorEndpoint { get; }

        public NetworkProfile NetworkRunProfile { get; }

        public NetworkProfileSetting ProfileSettings { get; }

        public string TrackingId { get; }

        public string ModuleId { get; }

        public string IotHubHostname { get; }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json")
                .AddEnvironmentVariables()
                .Build();

            var frequencies = new List<Frequency>();
            configuration.GetSection(FrequencyPropertyName).Bind(frequencies);

            IDictionary<string, NetworkProfile> defaultProfiles = new Dictionary<string, NetworkProfile>();
            configuration.GetSection(DefaultProfilesPropertyName).Bind(defaultProfiles);

            return new Settings(
                configuration.GetValue<TimeSpan>(StartAfterPropertyName),
                configuration.GetValue<string>(DockerUriPropertyName),
                configuration.GetValue<string>(NetworkIdPropertyName),
                frequencies,
                configuration.GetValue<string>(NetworkControllerRunProfilePropertyName),
                defaultProfiles,
                configuration.GetValue<Uri>(TestResultCoordinatorEndpointPropertyName),
                configuration.GetValue<string>(TrackingIdPropertyName),
                configuration.GetValue<string>(ModuleIdPropertyName),
                configuration.GetValue<string>(IotHubHostnamePropertyName));
        }
    }

    class Frequency
    {
        public TimeSpan OfflineFrequency { get; set; }

        public TimeSpan OnlineFrequency { get; set; }

        public int RunsCount { get; set; }
    }
}
