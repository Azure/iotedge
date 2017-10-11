// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;

    public interface IModuleWithIdentity : IEquatable<IModuleWithIdentity>
    {
        IModule Module { get; }

        IModuleIdentity ModuleIdentity { get; }
    }
}
