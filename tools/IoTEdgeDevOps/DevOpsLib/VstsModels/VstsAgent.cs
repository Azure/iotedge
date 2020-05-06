// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib.VstsModels
{
    using System.Collections.Generic;

    // Schema reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/distributedtask/agents/get?view=azure-devops-rest-5.1#taskagent
    public class VstsAgent
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }

        public bool Enabled { get; set; }

        public VstsAgentStatus Status { get; set; }

        public Dictionary<string, string> SystemCapabilities { get; set; }

        public Dictionary<string, string> UserCapabilities { get; set; }
    }
}
