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
        const string DefaultWebhostPort = "5001";
        const string DefaultResultSources = "";

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
                    configuration.GetValue<bool>("StorageOptimizeForPerformance", true),
                    configuration.GetValue<string>("ResultSources", DefaultResultSources));
            });
        Settings(string webhostPort, string storagePath, bool optimizeForPerformance, string resultSources)
        {
            this.WebhostPort = Preconditions.CheckNonWhiteSpace(webhostPort, nameof(webhostPort));
            this.StoragePath = storagePath;
            this.OptimizeForPerformance = Preconditions.CheckNotNull(optimizeForPerformance);
            this.ResultSources = this.ParseResultSources(Preconditions.CheckNonWhiteSpace(resultSources, nameof(resultSources)));
        }

        private List<ResultSource> ParseResultSources(string resultSourceString)
        {
            List<ResultSource> resultSourceList = new List<ResultSource>();
            string[] sourceTypePairs = resultSourceString.Split(";");
            foreach (string sourceTypePair in sourceTypePairs)
            {
                string[] sourceAndType = sourceTypePair.Split(",");
                if (sourceAndType.Length == 2)
                {
                    resultSourceList.Add(new ResultSource
                    {
                        Source = Preconditions.CheckNotNull(sourceAndType[0]),
                        Type = Preconditions.CheckNotNull(sourceAndType[1])
                    });
                }
            }

            return resultSourceList;
        }

        public static Settings Current => DefaultSettings.Value;

        public string WebhostPort { get; }

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
