// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports
{
    using Microsoft.Azure.Devices.Edge.Util;

    /// <summary>
    /// This is basic test result report implementation.
    /// </summary>
    abstract class TestResultReportBase : ITestResultReport
    {
        protected TestResultReportBase(string testDescription, string trackingId, string resultType)
        {
            this.TestDescription = Preconditions.CheckNonWhiteSpace(testDescription, nameof(testDescription));
            this.TrackingId = Preconditions.CheckNonWhiteSpace(trackingId, nameof(trackingId));
            this.ResultType = Preconditions.CheckNonWhiteSpace(resultType, nameof(resultType));
        }

        public string TestDescription { get; }

        public string TrackingId { get; }

        public abstract string Title { get; }

        public string ResultType { get; }

        public abstract bool IsPassed { get; }
    }
}
