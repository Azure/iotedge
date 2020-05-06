// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;

    public class TestInfoResult : TestResultBase
    {
        public TestInfoResult(string trackingId, string reportParty, string info, DateTime createdAt)
            : base(TestConstants.TestInfo.TestResultSource, TestOperationResultType.TestInfo, createdAt)
        {
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ReportParty = Preconditions.CheckNonWhiteSpace(reportParty, nameof(reportParty));
            this.Info = Preconditions.CheckNonWhiteSpace(info, nameof(info));
        }

        public string TrackingId { get; }

        public string ReportParty { get; }

        public string Info { get; }

        public override string GetFormattedResult() => this.ToPrettyJson();
    }
}
