// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;

    public enum ModuleStatus
    {
        Unknown,
        Running,
        Stopped
    }

    public interface IModule : IEquatable<IModule>
    {
        string Name { get; }

        string Version { get; }

        string Type { get; }

        ModuleStatus Status { get; }
    }

    public interface IModule<TConfig> : IModule, IEquatable<IModule<TConfig>>
    {
        TConfig Config { get; }
    }
}