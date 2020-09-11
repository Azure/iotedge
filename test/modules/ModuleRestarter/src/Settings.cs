// Copyright (c) Microsoft. All rights reserved.
namespace ModuleRestarter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    class Settings
    {
        internal static Settings Current = Create();

        Settings(
            string serviceClientConnectionString,
            string deviceId,
            string desiredModulesToRestartCSV,
            int restartIntervalInMins)
        {
            this.ServiceClientConnectionString = Preconditions.CheckNonWhiteSpace(serviceClientConnectionString, nameof(serviceClientConnectionString));
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
            this.RestartInterval = TimeSpan.FromMinutes(Preconditions.CheckRange(restartIntervalInMins, 1));

            // mitigate unintended repeated commas
            this.DesiredModulesToRestart = new List<string>();
            foreach (string name in desiredModulesToRestartCSV.Split(","))
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    this.DesiredModulesToRestart.Add(name);
                }
            }
        }

        static Settings Create()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/settings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return new Settings(
                configuration.GetValue("ServiceClientConnectionString", string.Empty),
                configuration.GetValue("IOTEDGE_DEVICEID", string.Empty),
                configuration.GetValue("DesiredModulesToRestartCSV", string.Empty),
                configuration.GetValue("RestartIntervalInMins", 10));
        }

        public string ServiceClientConnectionString { get; }

        public string DeviceId { get; }

        public List<string> DesiredModulesToRestart { get; }

        public TimeSpan RestartInterval { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>
            {
                { nameof(this.DeviceId), this.DeviceId },
                { nameof(this.DesiredModulesToRestart), string.Join(",", this.DesiredModulesToRestart) },
                { nameof(this.RestartInterval), this.RestartInterval.ToString() },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
