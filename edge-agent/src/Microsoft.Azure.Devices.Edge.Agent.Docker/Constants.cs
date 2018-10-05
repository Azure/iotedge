// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    static class Constants
    {
        public const string EdgeAgentCreateOptionsName = "EdgeAgentCreateOptions";

        public const string DefaultRegistryAddress = "https://index.docker.io/v1/";

        public const int TwinMaxValueSize = 512;

        public const int TwinValueMaxChunks = 100;   // The chunks sequence number is two bytes, which allows 100 chunks [0, 100)
    }
}
