// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.LegacyTwin
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Reports;

    /// <summary>
    /// This is a LegacyTwin report to show test results for LegacyTwin.
    /// </summary>
    class LegacyTwinReport : TestResultReportBase
    {
        public LegacyTwinReport(
            string testDescription,
            string trackingId,
            string resultType,
            string senderSource,
            IDictionary<int, int> results,
            bool isPassed)
            : base(testDescription, trackingId, resultType)
        {
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.Results = results;
            this.IsPassed = isPassed;
        }

        public string SenderSource { get; }

        public override string Title => $"LegacyTwin Report for [{this.SenderSource}]";

        public IDictionary<int, int> Results { get; }

        public override bool IsPassed { get; }
    }
}
