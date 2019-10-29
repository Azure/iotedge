// Copyright (c) Microsoft. All rights reserved.
namespace ModuleRestarter
{
    using System;
    using System.IO;
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
                    configuration.GetValue<string>("DesiredModulesToRestartCSV", string.Empty),
                    configuration.GetValue<int>("RestartIntervalInMins", 10));
            });

        Settings(
            string serviceClientConnectionString,
            string deviceId,
            string desiredModulesToRestartCSV,
            int restartIntervalInMins)
        {
            this.ServiceClientConnectionString = serviceClientConnectionString;
            this.DeviceId = deviceId;
            this.DesiredModulesToRestartCSV = desiredModulesToRestartCSV;
            this.RestartIntervalInMins = restartIntervalInMins;
        }

        public static Settings Current => DefaultSettings.Value;

        public string ServiceClientConnectionString { get; }

        public string DeviceId { get; }

        public string DesiredModulesToRestartCSV { get; }

        public int RestartIntervalInMins { get; }
    }
}
