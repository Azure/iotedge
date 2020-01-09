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
                    configuration.GetValue<string>("IOTEDGE_DEVICEID"),
                    configuration.GetValue<string>("TargetModuleId", "DirectMethodReceiver"),
                    configuration.GetValue<TransportType>("TransportType", TransportType.Amqp_Tcp_Only),
                    configuration.GetValue<TimeSpan>("DirectMethodDelay", TimeSpan.FromSeconds(5)),
                    Option.Maybe(configuration.GetValue<Uri>("ReportingEndpointUrl")),
                    configuration.GetValue<InvocationSource>("InvocationSource", InvocationSource.Local),
                    Option.Maybe<string>(configuration.GetValue<string>("ServiceClientConnectionString")),
                    configuration.GetValue<string>("IOTEDGE_MODULEID"),
                    configuration.GetValue("testDuration", TimeSpan.Zero),
                    configuration.GetValue("testStartDelay", TimeSpan.Zero),
                    Option.Maybe(configuration.GetValue<string>("DirectMethodName")),
                    Option.Maybe(configuration.GetValue<string>("trackingId")),
                    Option.Maybe(configuration.GetValue<string>("DirectMethodResultType")));
            });

        Settings(
            string deviceId,
            string targetModuleId,
            TransportType transportType,
            TimeSpan directMethodDelay,
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
            this.DirectMethodDelay = directMethodDelay;
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

        public static Settings Current => DefaultSettings.Value;

        public string DeviceId { get; }

        public string TargetModuleId { get; }

        public TransportType TransportType { get; }

        public TimeSpan DirectMethodDelay { get; }

        public InvocationSource InvocationSource { get; }

        public Option<string> ServiceClientConnectionString { get; }

        public Option<Uri> ReportingEndpointUrl { get; }

        public string ModuleId { get; }

        public TimeSpan TestDuration { get; }

        public TimeSpan TestStartDelay { get; }

        public string DirectMethodName { get; }

        public Option<string> TrackingId { get; }

        public DirectMethodResultType DirectMethodResultType { get; }

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
                { nameof(this.DirectMethodDelay), this.DirectMethodDelay.ToString() },
                { nameof(this.InvocationSource), this.InvocationSource.ToString() },
                { nameof(this.DirectMethodResultType), this.DirectMethodResultType.ToString() },
            };

            this.ReportingEndpointUrl.ForEach((url) =>
            {
                fields.Add(nameof(this.ReportingEndpointUrl), url.AbsoluteUri);
            });

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
