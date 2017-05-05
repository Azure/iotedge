// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Configuration;

    public static class ConfigHelper
    {
        const string Directory = "settings";

        static readonly Lazy<IConfiguration> TestConfigLazy = new Lazy<IConfiguration>(() => GetTestConfiguration(), true);
        static readonly Lazy<IConfiguration> TestEnvironmentConfigLazy = new Lazy<IConfiguration>(() => GetTestEnvironmentConfiguration(), true);
        static readonly Lazy<TestEnvironment> LazyEnvironment = new Lazy<TestEnvironment>(() => GetEnvironment(), true);

        public static IConfiguration TestConfig => TestConfigLazy.Value;

        public static IConfiguration KeyVaultConfig => TestConfig.GetSection("keyVault");

        public static TestEnvironment Environment => LazyEnvironment.Value;

        static IConfiguration GetTestConfiguration()
        {
            string env = Environment.ToString().ToLowerInvariant();
            string testEnvironmentFile = Path.Combine("settings", string.Concat(env, ".json"));

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(Directory, "base.json"), optional: true, reloadOnChange: true)
                .AddJsonFile(testEnvironmentFile, optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            return builder.Build();
        }

        static IConfiguration GetTestEnvironmentConfiguration()
        {
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(Directory, "environment.json"))
                .AddEnvironmentVariables();
            return builder.Build();
        }

        static TestEnvironment GetEnvironment()
        {
            string environmentName = TestEnvironmentConfigLazy.Value["testEnvironment"];
            if (!Enum.TryParse(environmentName, true, out TestEnvironment environment))
            {
                throw new InvalidOperationException($"Invalid test environment specified: {environmentName}");
            }
            return environment;
        }
    }
}