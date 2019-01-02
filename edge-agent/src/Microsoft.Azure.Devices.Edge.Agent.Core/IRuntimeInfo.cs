// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;

    public interface IRuntimeInfo : IEquatable<IRuntimeInfo>
    {
        string Type { get; }
    }

    public interface IRuntimeInfo<out TConfig> : IRuntimeInfo
    {
        TConfig Config { get; }
    }

    public class UnknownRuntimeInfo : IRuntimeInfo
    {
        UnknownRuntimeInfo() { }

        public static UnknownRuntimeInfo Instance { get; } = new UnknownRuntimeInfo();

        public string Type => Constants.Unknown;

        public bool Equals(IRuntimeInfo other) =>
            other != null && object.ReferenceEquals(this, other);
    }
}
