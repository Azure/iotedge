// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System.Text.RegularExpressions;

    public static class ConnectionStringHelper
    {
        public static string GetDeviceId(string connectionString)
        {
            var regex = new Regex("DeviceId=([^;]+)", RegexOptions.None);
            return regex.Match(connectionString).Groups[1].Value;
        }

        public static string GetHostName(string connectionString)
        {
            var regex = new Regex("HostName=([^;]+)", RegexOptions.None);
            return regex.Match(connectionString).Groups[1].Value;
        }

        public static string GetSharedAccessKey(string connectionString)
        {
            var regex = new Regex("SharedAccessKey=([^;]+)", RegexOptions.None);
            return regex.Match(connectionString).Groups[1].Value;
        }
    }
}
