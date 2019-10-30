// Copyright (c) Microsoft. All rights reserved.
namespace ModuleRestarter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
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
                    configuration.GetValue<string>("ServiceClientConnectionString"),
                    configuration.GetValue<string>("IOTEDGE_DEVICEID"),
                    configuration.GetValue<string>("DesiredModulesToRestartCSV"),
                    configuration.GetValue<int>("RestartIntervalInMins", 10));
            });

        Settings(
            string serviceClientConnectionString,
            string deviceId,
            string desiredModulesToRestartCSV,
            int restartIntervalInMins)
        {
            this.ServiceClientConnectionString = Preconditions.CheckNonWhiteSpace(serviceClientConnectionString, "ServiceClientConnectionString");
            this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, "DeviceId");
            this.RestartIntervalInMins = Preconditions.CheckRange(restartIntervalInMins, 0);

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

        public static Settings Current => DefaultSettings.Value;

        public string ServiceClientConnectionString { get; }

        public string DeviceId { get; }

        public List<string> DesiredModulesToRestart { get; }

        public int RestartIntervalInMins { get; }

        public override string ToString()
        {
            Dictionary<string, string> state = new Dictionary<string, string>();
            state.Add("DeviceId", this.DeviceId);
            state.Add("DesiredModulesToRestart", string.Join(",", this.DesiredModulesToRestart));
            state.Add("RestartInterval", this.RestartIntervalInMins.ToString());
            return JsonConvert.SerializeObject(state, Formatting.Indented);
        }
    }
}