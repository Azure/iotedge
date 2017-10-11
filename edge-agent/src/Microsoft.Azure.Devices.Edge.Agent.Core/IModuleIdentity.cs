// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;

    public interface IModuleIdentity : IEquatable<IModuleIdentity>
    {
        string Name { get;  }

        string ConnectionString { get; }
    }
}
