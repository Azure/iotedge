// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;

    public class DeploymentTestResult : TestResultBase
    {
        public DeploymentTestResult(string trackingId, string source, Dictionary<string, string> environmentVariables, DateTime createdAt)
            : base(source, TestOperationResultType.Deployment, createdAt)
        {
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.EnvironmentVariables = Preconditions.CheckNotNull(environmentVariables, nameof(environmentVariables));
        }

        public string TrackingId { get; }

        public Dictionary<string, string> EnvironmentVariables { get; }

        public override string GetFormattedResult() => this.ToPrettyJson();
    }
}
