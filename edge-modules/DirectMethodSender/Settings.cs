// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodSender
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
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
                    Option.Maybe(configuration.GetValue<Uri>("AnalyzerUrl")),
                    configuration.GetValue<string>("RoutingAgency", "EdgeHub"),
                    Option.Maybe<string>(configuration.GetValue<string>("ServiceClientConnectionString")));
            });

        Settings(
            string deviceId,
            string targetModuleId,
            TransportType transportType,
            TimeSpan directMethodDelay,
            Option<Uri> analyzerUrl,
            string routingAgency,
            Option<string> serviceClientConnectionString)
        {
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.TargetModuleId = Preconditions.CheckNonWhiteSpace(targetModuleId, nameof(targetModuleId));
            Preconditions.CheckArgument(TransportType.IsDefined(typeof(TransportType), transportType));
            this.TransportType = transportType;
            this.DirectMethodDelay = Preconditions.CheckNotNull(directMethodDelay);
            this.AnalyzerUrl = Preconditions.CheckNotNull(analyzerUrl);
            this.ServiceClientConnectionString = serviceClientConnectionString;

            object parsedRoutingAgency;
            Preconditions.CheckNonWhiteSpace(routingAgency, nameof(routingAgency));
            if (Enum.TryParse(typeof(RoutingAgency), routingAgency, true, out parsedRoutingAgency))
            {
                this.RoutingAgency = (RoutingAgency)parsedRoutingAgency;
            }
            else
            {
                throw new ArgumentException("Invalid RoutingAgency type");
            }
        }

        public static Settings Current => DefaultSettings.Value;

        public string DeviceId { get; }

        public string TargetModuleId { get; }

        public TransportType TransportType { get; }

        public TimeSpan DirectMethodDelay { get; }

        public RoutingAgency RoutingAgency { get; }

        public Option<string> ServiceClientConnectionString { get; }

        public Option<Uri> AnalyzerUrl { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>()
            {
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.TargetModuleId), this.TargetModuleId },
                { nameof(this.TransportType), Enum.GetName(typeof(TransportType), this.TransportType) },
                { nameof(this.DirectMethodDelay), this.DirectMethodDelay.ToString() },
                { nameof(this.RoutingAgency), this.RoutingAgency.ToString() },
            };

            this.AnalyzerUrl.ForEach((url) =>
            {
                fields.Add(nameof(this.AnalyzerUrl), url.AbsoluteUri);
            });

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
