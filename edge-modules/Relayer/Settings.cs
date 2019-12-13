// Copyright (c) Microsoft. All rights reserved.
namespace Relayer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
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
                    configuration.GetValue("transportType", TransportType.Amqp_Tcp_Only),
                    configuration.GetValue("inputName", "input1"),
                    configuration.GetValue("outputName", "output1"),
                    configuration.GetValue<Uri>("testResultCoordinatorUrl", new Uri("http://testresultcoordinator:5001")),
                    configuration.GetValue<string>("IOTEDGE_MODULEID"));
            });

        Settings(
            TransportType transportType,
            string inputName,
            string outputName,
            Uri testResultCoordinatorUrl,
            string moduleId)
        {
            this.InputName = Preconditions.CheckNonWhiteSpace(inputName, nameof(inputName));
            this.OutputName = Preconditions.CheckNonWhiteSpace(outputName, nameof(outputName));
            this.TransportType = transportType;
            this.TestResultCoordinatorUrl = Preconditions.CheckNotNull(testResultCoordinatorUrl, nameof(testResultCoordinatorUrl));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
        }

        public static Settings Current => DefaultSettings.Value;

        [JsonConverter(typeof(StringEnumConverter))]
        public TransportType TransportType { get; }

        public string InputName { get; }

        public string OutputName { get; }

        public Uri TestResultCoordinatorUrl { get; }

        public string ModuleId { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>
            {
                { nameof(this.InputName), this.InputName },
                { nameof(this.OutputName), this.OutputName },
                { nameof(this.ModuleId), this.ModuleId },
                { nameof(this.TransportType), Enum.GetName(typeof(TransportType), this.TransportType) },
                { nameof(this.TestResultCoordinatorUrl), this.TestResultCoordinatorUrl.ToString() },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
