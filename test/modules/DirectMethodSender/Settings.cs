// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
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
            string deviceId,
            string targetModuleId,
            TransportType transportType,
            TimeSpan directMethodFrequency,
            Option<Uri> reportingEndpointUrl,
            InvocationSource invocationSource,
            Option<string> serviceClientConnectionString,
            string moduleId,
            TimeSpan testDuration,
            TimeSpan testStartDelay,
            Option<string> directMethodName,
            Option<string> trackingId,
            Option<string> directMethodResultType)
        {
            Preconditions.CheckRange(testDuration.Ticks, 0);
            Preconditions.CheckRange(testStartDelay.Ticks, 0);

            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.TargetModuleId = Preconditions.CheckNonWhiteSpace(targetModuleId, nameof(targetModuleId));
            Preconditions.CheckArgument(TransportType.IsDefined(typeof(TransportType), transportType));
            this.TransportType = transportType;
            this.DirectMethodFrequency = directMethodFrequency;
            this.InvocationSource = invocationSource;
            this.ReportingEndpointUrl = reportingEndpointUrl;
            this.ServiceClientConnectionString = serviceClientConnectionString;
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.TestDuration = testDuration;
            this.TestStartDelay = testStartDelay;
            this.DirectMethodName = directMethodName.GetOrElse("HelloWorldMethod");
            this.TrackingId = trackingId;
            this.DirectMethodResultType = (DirectMethodResultType)Enum.Parse(typeof(DirectMethodResultType), directMethodResultType.GetOrElse("LegacyDirectMethodTestResult"));
        }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return new Settings(
                configuration.GetValue<string>("IOTEDGE_DEVICEID"),
                configuration.GetValue<string>("TargetModuleId", "DirectMethodReceiver"),
                configuration.GetValue<TransportType>("TransportType", TransportType.Amqp_Tcp_Only),
                configuration.GetValue<TimeSpan>("DirectMethodFrequency", TimeSpan.FromSeconds(5)),
                Option.Maybe(configuration.GetValue<Uri>("ReportingEndpointUrl")),
                configuration.GetValue<InvocationSource>("InvocationSource", InvocationSource.Local),
                Option.Maybe<string>(configuration.GetValue<string>("IOT_HUB_CONNECTION_STRING")),
                configuration.GetValue<string>("IOTEDGE_MODULEID"),
                configuration.GetValue("testDuration", TimeSpan.Zero),
                configuration.GetValue("testStartDelay", TimeSpan.Zero),
                Option.Maybe(configuration.GetValue<string>("DirectMethodName")),
                Option.Maybe(configuration.GetValue<string>("trackingId")),
                Option.Maybe(configuration.GetValue<string>("DirectMethodResultType")));
        }

        internal string DeviceId { get; }

        internal string TargetModuleId { get; }

        internal TransportType TransportType { get; }

        internal TimeSpan DirectMethodFrequency { get; }

        internal InvocationSource InvocationSource { get; }

        internal Option<string> ServiceClientConnectionString { get; }

        internal Option<Uri> ReportingEndpointUrl { get; }

        internal string ModuleId { get; }

        internal TimeSpan TestDuration { get; }

        internal TimeSpan TestStartDelay { get; }

        internal string DirectMethodName { get; }

        internal Option<string> TrackingId { get; }

        internal DirectMethodResultType DirectMethodResultType { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>()
            {
                { nameof(this.ModuleId), this.ModuleId },
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.TargetModuleId), this.TargetModuleId },
                { nameof(this.TestDuration), this.TestDuration.ToString() },
                { nameof(this.TestStartDelay), this.TestStartDelay.ToString() },
                { nameof(this.TrackingId), this.TrackingId.ToString() },
                { nameof(this.TransportType), Enum.GetName(typeof(TransportType), this.TransportType) },
                { nameof(this.DirectMethodName), this.DirectMethodName },
                { nameof(this.DirectMethodFrequency), this.DirectMethodFrequency.ToString() },
                { nameof(this.InvocationSource), this.InvocationSource.ToString() },
                { nameof(this.DirectMethodResultType), this.DirectMethodResultType.ToString() },
                { nameof(this.ReportingEndpointUrl), this.ReportingEndpointUrl.ToString() },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
