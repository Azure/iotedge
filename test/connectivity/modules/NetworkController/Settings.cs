// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
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
        const string ParentHostnamePropertyName = "IOTEDGE_PARENTHOSTNAME";
        const string DefaultProfilesPropertyName = "DefaultProfiles";
        const string TransportTypePropertyName = "TransportType";

        Settings(
            TimeSpan startAfter,
            string dockerUri,
            string networkId,
            IList<Frequency> frequencies,
            NetworkProfile runProfileSettings,
            Uri testResultCoordinatorEndpoint,
            string trackingId,
            string moduleId,
            string iothubHostname,
            string parentHostname,
            TransportType transportType)
        {
            this.StartAfter = startAfter;
            this.Frequencies = frequencies;
            this.DockerUri = Preconditions.CheckNonWhiteSpace(dockerUri, nameof(dockerUri));
            this.NetworkId = Preconditions.CheckNonWhiteSpace(networkId, nameof(networkId));
            this.TestResultCoordinatorEndpoint = Preconditions.CheckNotNull(testResultCoordinatorEndpoint, nameof(testResultCoordinatorEndpoint));
            this.NetworkRunProfile = Preconditions.CheckNotNull(runProfileSettings, nameof(runProfileSettings));
            Preconditions.CheckRange<uint>(this.NetworkRunProfile.ProfileSetting.PackageLoss, 0, 101, nameof(this.NetworkRunProfile));
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.IotHubHostname = Preconditions.CheckNonWhiteSpace(iothubHostname, nameof(iothubHostname));
            this.ParentHostname = Preconditions.CheckNotNull(parentHostname, nameof(parentHostname));
            this.TransportType = transportType;
        }

        public TimeSpan StartAfter { get; }

        public IList<Frequency> Frequencies { get; }

        public string DockerUri { get; }

        public string NetworkId { get; }

        public Uri TestResultCoordinatorEndpoint { get; }

        public NetworkProfile NetworkRunProfile { get; }

        public string TrackingId { get; }

        public string ModuleId { get; }

        public string IotHubHostname { get; }

        public string ParentHostname { get; }

        public TransportType TransportType { get; }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json")
                .AddEnvironmentVariables()
                .Build();

            var frequencies = new List<Frequency>();
            configuration.GetSection(FrequencyPropertyName).Bind(frequencies);

            string runProfileName = configuration.GetValue<string>(TestConstants.NetworkController.RunProfilePropertyName);
            NetworkProfile runProfileSettings = configuration.GetSection($"{DefaultProfilesPropertyName}:{runProfileName}").Get<NetworkProfile>();
            if (runProfileSettings == null)
            {
                runProfileSettings = NetworkProfile.Online;
            }

            return new Settings(
                configuration.GetValue<TimeSpan>(StartAfterPropertyName),
                configuration.GetValue<string>(DockerUriPropertyName),
                configuration.GetValue<string>(NetworkIdPropertyName),
                frequencies,
                runProfileSettings,
                configuration.GetValue<Uri>(TestResultCoordinatorEndpointPropertyName),
                configuration.GetValue<string>(TrackingIdPropertyName),
                configuration.GetValue<string>(ModuleIdPropertyName),
                configuration.GetValue<string>(IotHubHostnamePropertyName),
                configuration.GetValue<string>(ParentHostnamePropertyName, string.Empty),
                configuration.GetValue(TransportTypePropertyName, TransportType.Amqp_Tcp_Only));
        }
    }

    class Frequency
    {
        public TimeSpan OfflineFrequency { get; set; }

        public TimeSpan OnlineFrequency { get; set; }

        public int RunsCount { get; set; }
    }
}
