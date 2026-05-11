// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.Collections.Generic;

    public static class SecretsHelper
    {
        public static string GetSecret(string secret)
        {
            Preconditions.CheckNonWhiteSpace(secret, nameof(secret));
            return ConfigHelper.TestConfig[secret] ?? throw new KeyNotFoundException($"Secret '{secret}' not found in configuration.");
        }

        public static string GetSecretFromConfigKey(string configName)
        {
            string configValue = ConfigHelper.TestConfig[Preconditions.CheckNonWhiteSpace(configName, nameof(configName))];
            return GetSecret(configValue);
        }
    }
}
