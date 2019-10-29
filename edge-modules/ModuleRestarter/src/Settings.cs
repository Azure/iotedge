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
            this.DesiredModulesToRestartCSV = Preconditions.CheckNonWhiteSpace(desiredModulesToRestartCSV, "DesiredModulesToRestartCSV");
            this.RestartIntervalInMins = Preconditions.CheckRange(restartIntervalInMins, 0);
        }

        public static Settings Current => DefaultSettings.Value;

        public string ServiceClientConnectionString { get; }

        public string DeviceId { get; }

        private string DesiredModulesToRestartCSV { get; }

        public int RestartIntervalInMins { get; }

        public List<string> GetDesiredModulesToRestart()
        {
            // mitigate unintended repeated commas
            List<string> moduleNames = new List<string>();
            foreach (string name in this.DesiredModulesToRestartCSV.Split(","))
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    moduleNames.Add(name);
                }
            }

            if (moduleNames.Count == 0)
            {
                throw new ArgumentException("No module names specified");
            }

            return moduleNames;
        }

        public override string ToString()
        {
            Dictionary<string, string> state = new Dictionary<string, string>();
            state.Add("DeviceId", this.DeviceId);
            state.Add("ServiceClientConnectionString", this.ServiceClientConnectionString);
            state.Add("DesiredModulesToRestart", string.Join(",", this.GetDesiredModulesToRestart()));
            state.Add("RestartInterval", this.RestartIntervalInMins.ToString());
            return JsonConvert.SerializeObject(state, Formatting.Indented);
        }
    }
}
