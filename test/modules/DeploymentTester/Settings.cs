// Copyright (c) Microsoft. All rights reserved.
namespace DeploymentTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    class Settings
    {
        public const string EnvironmentVariablePrefix = "IOTEDGE_DT";

        static readonly Lazy<Settings> DefaultSettings = new Lazy<Settings>(
            () =>
            {
                IConfiguration configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("config/settings.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                return new Settings(
                    configuration.GetValue<string>("IOTEDGE_DEVICEID"),
                    configuration.GetValue<string>("IOTEDGE_MODULEID"),
                    configuration.GetValue("DEPLOYMENT_TESTER_MODE", DeploymentTesterMode.Receiver),
                    Option.Maybe(configuration.GetValue<string>("IOT_HUB_CONNECTION_STRING")),
                    configuration.GetValue("testResultCoordinatorUrl", new Uri("http://testresultcoordinator:5001")),
                    configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)),
                    configuration.GetValue("testDuration", TimeSpan.FromHours(1)),
                    configuration.GetValue<string>("trackingId"),
                    Option.Maybe(configuration.GetValue<string>("targetModuleId")),
                    configuration.GetValue("DEPLOYMENT_UPDATE_PERIOD", TimeSpan.FromMinutes(3)));
            });

        Settings(
            string deviceId,
            string moduleId,
            DeploymentTesterMode testMode,
            Option<string> iotHubConnectionString,
            Uri testResultCoordinatorUrl,
            TimeSpan testStartDelay,
            TimeSpan testDuration,
            string trackingId,
            Option<string> targetModuleId,
            TimeSpan deploymentUpdatePeriod)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.TestResultCoordinatorUrl = Preconditions.CheckNotNull(testResultCoordinatorUrl, nameof(testResultCoordinatorUrl));
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));

            if (testMode == DeploymentTesterMode.Sender)
            {
                Preconditions.CheckArgument(iotHubConnectionString.HasValue, nameof(iotHubConnectionString));
                this.IoTHubConnectionString = iotHubConnectionString;

                Preconditions.CheckArgument(targetModuleId.HasValue, nameof(targetModuleId));
                this.TargetModuleId = targetModuleId;
            }

            this.TestMode = testMode;
            this.TestStartDelay = testStartDelay;
            this.TestDuration = testDuration;
            this.DeploymentUpdatePeriod = deploymentUpdatePeriod;
        }

        internal static Settings Current => DefaultSettings.Value;

        public string DeviceId { get; }

        public string ModuleId { get; }

        public DeploymentTesterMode TestMode { get; }

        public Option<string> IoTHubConnectionString { get; }

        public Uri TestResultCoordinatorUrl { get; }

        public TimeSpan TestStartDelay { get; }

        public TimeSpan TestDuration { get; }

        public string TrackingId { get; }

        public Option<string> TargetModuleId { get; set; }

        public TimeSpan DeploymentUpdatePeriod { get; set; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>
            {
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.DeploymentUpdatePeriod), this.DeploymentUpdatePeriod.ToString() },
                { nameof(this.ModuleId), this.ModuleId },
                { nameof(this.TestMode), this.TestMode.ToString() },
                { nameof(this.TestResultCoordinatorUrl), this.TestResultCoordinatorUrl.ToString() },
                { nameof(this.TestStartDelay), this.TestStartDelay.ToString() },
                { nameof(this.TestDuration), this.TestDuration.ToString() },
                { nameof(this.TrackingId), this.TrackingId },
                { nameof(this.TargetModuleId), this.TargetModuleId.ToString() },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
