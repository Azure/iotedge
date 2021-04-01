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

    class Settings
    {
        internal static Settings Current = Create();

        Settings(
            TransportType transportType,
            string inputName,
            string outputName,
            Uri testResultCoordinatorUrl,
            string moduleId,
            bool receiveOnly,
            bool enableTrcReporting,
            Option<int> uniqueResultsExpected)
        {
            this.InputName = Preconditions.CheckNonWhiteSpace(inputName, nameof(inputName));
            this.OutputName = Preconditions.CheckNonWhiteSpace(outputName, nameof(outputName));
            this.TransportType = transportType;
            this.TestResultCoordinatorUrl = Preconditions.CheckNotNull(testResultCoordinatorUrl, nameof(testResultCoordinatorUrl));
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.ReceiveOnly = receiveOnly;
            this.EnableTrcReporting = enableTrcReporting;
            this.UniqueResultsExpected = uniqueResultsExpected;
        }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            int uniqueResultsNum = configuration.GetValue<int>("uniqueResultsExpected", -1);
            Option<int> uniqueResultsExpected = uniqueResultsNum > 0 ? Option.Some(uniqueResultsNum) : Option.None<int>();

            return new Settings(
                configuration.GetValue("transportType", TransportType.Amqp_Tcp_Only),
                configuration.GetValue("inputName", "input1"),
                configuration.GetValue("outputName", "output1"),
                configuration.GetValue<Uri>("testResultCoordinatorUrl", new Uri("http://testresultcoordinator:5001")),
                configuration.GetValue<string>("IOTEDGE_MODULEID"),
                configuration.GetValue<bool>("receiveOnly", false),
                configuration.GetValue<bool>("enableTrcReporting", true),
                uniqueResultsExpected);
        }

        public TransportType TransportType { get; }

        public string InputName { get; }

        public string OutputName { get; }

        public bool EnableTrcReporting { get; }

        public Uri TestResultCoordinatorUrl { get; }

        public string ModuleId { get; }

        public bool ReceiveOnly { get; }

        public Option<int> UniqueResultsExpected { get; }

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
                { nameof(this.ReceiveOnly), this.ReceiveOnly.ToString() },
                { nameof(this.EnableTrcReporting), this.EnableTrcReporting.ToString() },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
