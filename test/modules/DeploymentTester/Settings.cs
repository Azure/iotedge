// Copyright (c) Microsoft. All rights reserved.
namespace DeploymentTester
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public class Settings
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
                    configuration.GetValue<string>("IOT_HUB_CONNECTION_STRING"),
                    configuration.GetValue("testResultCoordinatorUrl", new Uri("http://testresultcoordinator:5001")),
                    configuration.GetValue("testStartDelay", TimeSpan.FromMinutes(2)),
                    configuration.GetValue("testDuration", TimeSpan.FromHours(1)),
                    configuration.GetValue<string>("trackingId"),
                    configuration.GetValue<string>("targetModuleId"),
                    configuration.GetValue("DEPLOYMENT_UPDATE_PERIOD", TimeSpan.FromMinutes(3)));
            });

        Settings(
            string deviceId,
            string moduleId,
            DeploymentTesterMode testMode,
            string iotHubConnectionString,
            Uri testResultCoordinatorUrl,
            TimeSpan testStartDelay,
            TimeSpan testDuration,
            string trackingId,
            string targetModuleId,
            TimeSpan deploymentUpdatePeriod)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.IoTHubConnectionString = Preconditions.CheckNonWhiteSpace(iotHubConnectionString, nameof(iotHubConnectionString));
            this.TestResultCoordinatorUrl = Preconditions.CheckNotNull(testResultCoordinatorUrl, nameof(testResultCoordinatorUrl));
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.TargetModuleId = Preconditions.CheckNonWhiteSpace(targetModuleId, nameof(targetModuleId));

            this.TestMode = testMode;
            this.TestStartDelay = testStartDelay;
            this.TestDuration = testDuration;
            this.DeploymentUpdatePeriod = deploymentUpdatePeriod;
        }

        public static Settings Current => DefaultSettings.Value;

        public string DeviceId { get; }

        public string ModuleId { get; }

        public DeploymentTesterMode TestMode { get; }

        public string IoTHubConnectionString { get; }

        public Uri TestResultCoordinatorUrl { get; }

        public TimeSpan TestStartDelay { get; }

        public TimeSpan TestDuration { get; }

        public string TrackingId { get; }

        public string TargetModuleId { get; set; }

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
                { nameof(this.TargetModuleId), this.TargetModuleId },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
