// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class AuthConfig
    {
        public AuthConfig(string name)
        {
            this.Name = Preconditions.CheckNonWhiteSpace(name, nameof(name));
        }

        public string Name { get; }
    }
}
