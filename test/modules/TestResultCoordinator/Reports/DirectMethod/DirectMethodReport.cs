// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod
{
    using TestResultCoordinator.Reports;

    /// <summary>
    /// This is a DirectMethod report to show test results for DirectMethods.
    /// </summary>
    class DirectMethodReport : TestResultReportBase
    {
        public DirectMethodReport(
            string trackingId,
            string expectedSource,
            string actualSource,
            string resultType,
            ulong networkOnSuccess,
            ulong networkOffSuccess,
            ulong networkOnToleratedSuccess,
            ulong networkOffToleratedSuccess,
            ulong networkOnFailure,
            ulong networkOffFailure,
            ulong mismatchSuccess,
            ulong mismatchFailure)
            : base(trackingId, expectedSource, actualSource, resultType)
        {
            this.NetworkOnSuccess = networkOnSuccess;
            this.NetworkOffSuccess = networkOffSuccess;
            this.NetworkOnToleratedSuccess = networkOnToleratedSuccess;
            this.NetworkOffToleratedSuccess = networkOffToleratedSuccess;
            this.NetworkOnFailure = networkOnFailure;
            this.NetworkOffFailure = networkOffFailure;
            this.MismatchSuccess = mismatchSuccess;
            this.MismatchFailure = mismatchFailure;
        }

        public ulong NetworkOnSuccess { get; }

        public ulong NetworkOffSuccess { get; }

        public ulong NetworkOnToleratedSuccess { get; }

        public ulong NetworkOffToleratedSuccess { get; }

        public ulong NetworkOnFailure { get; }

        public ulong NetworkOffFailure { get; }

        public ulong MismatchSuccess { get; }

        public ulong MismatchFailure { get; }

        public override bool IsPassed => throw new System.NotImplementedException();
    }
}
