// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class Settings
    {
        const string DefaultStoragePath = "";
        const ushort DefaultWebhostPort = 5001;

        static readonly Lazy<Settings> DefaultSettings = new Lazy<Settings>(
            () =>
            {
                IConfiguration configuration = new ConfigurationBuilder()
                   .SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile("config/settings.json")
                   .AddEnvironmentVariables()
                   .Build();

                return new Settings(
                    configuration.GetValue("WebhostPort", DefaultWebhostPort),
                    configuration.GetValue<string>("StoragePath", DefaultStoragePath),
                    configuration.GetValue<bool>("StorageOptimizeForPerformance", true));
            });
        Settings(ushort webhostPort, string storagePath, bool optimizeForPerformance)
        {
            this.WebhostPort = Preconditions.CheckNotNull(webhostPort);
            this.StoragePath = storagePath;
            this.OptimizeForPerformance = Preconditions.CheckNotNull(optimizeForPerformance);
            this.ResultSources = this.ParseResultSources();
        }

        List<ResultSource> ParseResultSources()
        {
            // TODO: Remove this hardcoded list and use environment variables once we've decided on how exactly to set the configuration
            List<string> sourceNames = new List<string> { "loadGen1.send", "relayer1.receive", "relayer1.send", "loadGen1.eventHub", "loadGen2.send", "relayer2.receive", "relayer2.send", "loadGen2.eventHub" };
            string resultType = "messages";
            List<ResultSource> resultSources = new List<ResultSource>();
            foreach (string sourceName in sourceNames)
            {
                resultSources.Add(new ResultSource
                {
                    Source = sourceName,
                    Type = resultType
                });
            }

            return resultSources;
        }

        public static Settings Current => DefaultSettings.Value;

        public ushort WebhostPort { get; }

        public string StoragePath { get; }

        public bool OptimizeForPerformance { get; }

        public List<ResultSource> ResultSources { get; }

        public override string ToString()
        {
            // serializing in this pattern so that secrets don't accidentally get added anywhere in the future
            var fields = new Dictionary<string, string>()
            {
                { nameof(this.WebhostPort), this.WebhostPort.ToString() },
                { nameof(this.StoragePath), this.StoragePath.ToString() },
                { nameof(this.OptimizeForPerformance), this.OptimizeForPerformance.ToString() },
                { nameof(this.ResultSources), string.Join("\n", this.ResultSources) },
            };

            return $"Settings:{Environment.NewLine}{string.Join(Environment.NewLine, fields.Select(f => $"{f.Key}={f.Value}"))}";
        }
    }
}
