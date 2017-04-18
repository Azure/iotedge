// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    public interface IModule
    {
        string Name { get; }

        string Type { get; }
    }
}