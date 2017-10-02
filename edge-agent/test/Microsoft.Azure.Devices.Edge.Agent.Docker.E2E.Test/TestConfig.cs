// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.E2E.Test
{
    public class TestConfig
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public string ImageName { get; set; }

        public string ImageTag { get; set; }

        public string ImageCreateOptions { get; set; }

        public Validator Validator { get; set; }
    }
}
