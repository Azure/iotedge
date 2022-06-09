// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LegacyTwinTestResult : TestResultBase
    {
        public LegacyTwinTestResult(
            string source,
            DateTime createdAt,
            string status)
            : base(source, TestOperationResultType.LegacyTwin, createdAt)
        {
            this.Status = Preconditions.CheckNonWhiteSpace(status, nameof(status));
        }

        public string Status { get; set; }

        public override string GetFormattedResult()
        {
            return this.Status;
        }
    }
}
