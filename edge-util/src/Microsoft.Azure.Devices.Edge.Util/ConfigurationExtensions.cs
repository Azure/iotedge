// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public static class ConfigurationExtensions
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger(typeof(ConfigurationExtensions));

        public static T GetValueIgnoreException<T>(this IConfiguration configuration, string key, T defaultValue)
        {
            try
            {
                return configuration.GetValue(key, defaultValue);
            }
            catch (Exception ex)
            {
                Log.LogWarning(ex, $"Could not get configuration for {key}");
                return defaultValue;
            }
        }

    }
}
