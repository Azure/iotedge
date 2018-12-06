// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models
{
    using System.Collections.Generic;

    public class ModuleSpec
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public object Settings { get; set; }

        public IEnumerable<EnvVar> EnvironmentVariables { get; set; }
    }
}
