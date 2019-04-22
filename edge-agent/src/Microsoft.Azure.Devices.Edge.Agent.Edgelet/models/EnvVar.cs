// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class EnvVar
    {
        public EnvVar(string key, string value)
        {
            this.Key = Preconditions.CheckNonWhiteSpace(key, nameof(key));
            this.Value = Preconditions.CheckNonWhiteSpace(value, nameof(value));
        }

        public string Key { get; }

        public string Value { get; }
    }
}
