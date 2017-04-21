// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Threading;

    public static class ConfigHelper
    {
        static readonly Lazy<IConfiguration> AppConfigLazy = new Lazy<IConfiguration>(
            () =>
            {
                IConfigurationBuilder builder = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();
                IConfiguration config = builder.Build();
                return config;
            },
            LazyThreadSafetyMode.ExecutionAndPublication);

        public static IConfiguration AppConfig => AppConfigLazy.Value;

        public static IConfiguration KeyVaultConfig => AppConfigLazy.Value.GetSection("keyVault");
    }
}
