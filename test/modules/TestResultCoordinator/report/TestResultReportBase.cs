// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Report
{
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is basic test result report implementation.
    /// </summary>
    abstract class TestResultReportBase : ITestResultReport
    {
        protected TestResultReportBase(string trackingId, string expectedSource, string actualSource, string resultType)
        {
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ExpectSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
        }

        public string TrackingId { get; }

        public string Title => $"Counting Report ({this.ResultType}) between [{this.ExpectSource}] and [{this.ActualSource}]";

        public string ResultType { get; }

        public string ExpectSource { get; }

        public string ActualSource { get; }
    }
}
