// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    public enum Topology
    {
        // A single edge device.
        SingleNode,

        // Multiple edge devices connected together in some toplogy.
        // Runs an extended test suite relative to `SingleNode`.
        Nested
    }
}
