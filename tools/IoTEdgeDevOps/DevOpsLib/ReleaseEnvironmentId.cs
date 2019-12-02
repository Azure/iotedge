// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;

    public class ReleaseEnvironmentId
    {
        // TODO: Create a class to manage release environment id and relationship with release definition
        public Dictionary<int, string> DefinitionIdToDisplayNameMapping = new Dictionary<int, string>
        {
            { 10073, "Linux AMD64 Docker" },
            { 10538, "Linux AMD64 Moby" },
            { 10075, "RBPi ARM32 Moby" },
            { 28576, "RBPi ARM64 Docker" },
            { 13148, "VM Proxy" },
            { 15664, "WinPro-x64" },
            { 37326, "WinSvr-x64" },
            { 50386, "WinIoT-x64" },
            { 51994, "WinIoT-ARM32" }
        };
    }
}
