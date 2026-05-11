// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Threading.Tasks;

    public static class SecretsHelper
    {
        public static string GetSecret(string secret)
        {
            Preconditions.CheckNonWhiteSpace(secret, nameof(secret));
            return ConfigHelper.TestConfig[secret] ?? string.Empty;
        }

        public static string GetSecretFromConfigKey(string configName)
        {
            string configValue = ConfigHelper.TestConfig[Preconditions.CheckNonWhiteSpace(configName, nameof(configName))];
            return GetSecret(configValue);
        }
    }
}
