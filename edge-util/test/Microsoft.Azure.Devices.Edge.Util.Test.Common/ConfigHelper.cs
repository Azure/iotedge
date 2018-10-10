// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Configuration;

    public static class ConfigHelper
    {
        const string Directory = "settings";

        static readonly Lazy<IConfigurationRoot> TestConfigLazy = new Lazy<IConfigurationRoot>(() => GetTestConfiguration(), true);
        static readonly Lazy<IConfigurationRoot> TestEnvironmentConfigLazy = new Lazy<IConfigurationRoot>(() => GetTestEnvironmentConfiguration(), true);
        static readonly Lazy<TestEnvironment> LazyEnvironment = new Lazy<TestEnvironment>(() => GetEnvironment(), true);

        public static IConfigurationRoot TestConfig => TestConfigLazy.Value;

        public static IConfiguration KeyVaultConfig => TestConfig.GetSection("keyVault");

        public static TestEnvironment Environment => LazyEnvironment.Value;

        static IConfigurationRoot GetTestConfiguration()
        {
            string env = Environment.ToString().ToLowerInvariant();
            string testEnvironmentFile = Path.Combine("settings", string.Concat(env, ".json"));

            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(Directory, "base.json"), optional: true, reloadOnChange: true)
                .AddJsonFile(testEnvironmentFile, optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            return builder.Build();
        }

        static IConfigurationRoot GetTestEnvironmentConfiguration()
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
