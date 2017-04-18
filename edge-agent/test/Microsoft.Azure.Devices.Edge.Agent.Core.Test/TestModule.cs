// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;

    public class TestModule : IModule
    {
        public string Name { get; }

        public string Type { get; }

        public TestModule(string name, string type)
        {
            this.Name = name;
            this.Type = type;
        }
    }
}