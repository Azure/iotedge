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

        public static TimeSpan GetTimeSpan(this IConfiguration configuration, string key, TimeSpan defaultValue)
        {
            try
            {
                return configuration.GetValue(key, defaultValue);
            }
            catch (Exception ex)
            when (ex is OverflowException || ex is FormatException)
            {
                Log.LogWarning(ex, $"Could not parse timespan for {key}");
                return defaultValue;
            }
        }
    }
}
