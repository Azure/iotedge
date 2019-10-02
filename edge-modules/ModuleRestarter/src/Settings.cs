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
                    configuration.GetValue<string>("IoTHubConnectionString"),
                    configuration.GetValue<string>("DeviceId"),
                    configuration.GetValue("DesiredModulesToRestartCSV", string.Empty),
                    configuration.GetValue<int>("RandomRestartIntervalInMins", 20));
            });

        Settings(
            string ioTHubConnectionString,
            string deviceId,
            string desiredModulesToRestartCSV,
            int randomRestartIntervalInMins)
        {
            this.IoTHubConnectionString = ioTHubConnectionString;
            this.DeviceId = deviceId;
            this.DesiredModulesToRestartCSV = desiredModulesToRestartCSV;
            this.RandomRestartIntervalInMins = randomRestartIntervalInMins;
        }

        public static Settings Current => DefaultSettings.Value;

        public string IoTHubConnectionString { get; }

        public string DeviceId { get; }

        public string DesiredModulesToRestartCSV { get; }

        public int RandomRestartIntervalInMins { get; }
    }
}