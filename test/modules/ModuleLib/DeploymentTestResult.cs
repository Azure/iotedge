// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil
{
    using System.Collections.Generic;

    public class DeploymentTestResult
    {
        public string TrackingId { get; set; }

        public Dictionary<string, string> EnvironmentVariables { get; set; }
    }
}
