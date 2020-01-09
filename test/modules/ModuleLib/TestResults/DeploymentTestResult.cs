// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Json;

    public class DeploymentTestResult : TestResultBase
    {
        public DeploymentTestResult(string source, DateTime createdAt)
            : base(source, TestOperationResultType.Deployment, createdAt)
        {
            this.EnvironmentVariables = new Dictionary<string, string>();
        }

        public string TrackingId { get; set; }

        public Dictionary<string, string> EnvironmentVariables { get; set; }

        public override string GetFormattedResult() => this.ToPrettyJson();
    }
}
