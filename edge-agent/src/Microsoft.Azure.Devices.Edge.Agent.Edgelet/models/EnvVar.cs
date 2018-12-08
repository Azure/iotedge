// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.Devices.Edge.Util;

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models
{
    public class EnvVar
    {
        public string Key { get; }

        public string Value { get; }

        public EnvVar(string key, string value)
        {
            this.Key = Preconditions.CheckNonWhiteSpace(key, nameof(key));
            this.Value = Preconditions.CheckNonWhiteSpace(value, nameof(value));
        }
    }
}
