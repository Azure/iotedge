// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class Settings
    {
        const string DefaultStoragePath = "";

        static readonly Lazy<Settings> Setting = new Lazy<Settings>(
            () =>
            {
                IConfiguration configuration = new ConfigurationBuilder()
                   .SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile("config/settings.json")
                   .AddEnvironmentVariables()
                   .Build();

                return new Settings(
                    configuration.GetValue<string>("storagePath", DefaultStoragePath),
                    configuration.GetValue<bool>("StorageOptimizeForPerformance", true));
            });
        Settings(string storagePath, bool optimizeForPerformance)
        {
            this.StoragePath = storagePath;
            this.OptimizeForPerformance = optimizeForPerformance;
        }

        public static Settings Current => Setting.Value;

        public string StoragePath { get; }

        public bool OptimizeForPerformance { get; }
    }
}
