// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public static class ConfigurationEx
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger(typeof(ConfigurationEx));

        public static TimeSpan GetTimeSpan(this IConfiguration configuration, string key, TimeSpan defaultValue)
        {
            try
            {
                return configuration.GetValue(key, defaultValue);
            }
            catch (Exception ex)
            when (IsParseException(ex))
            {
                Log.LogWarning(ex, $"Could not parse timespan for {key}");
                return defaultValue;
            }
        }

        static bool IsParseException(Exception ex)
        {
            return ex != null && (ex is OverflowException || ex is FormatException || IsParseException(ex.InnerException));
        }
    }
}
