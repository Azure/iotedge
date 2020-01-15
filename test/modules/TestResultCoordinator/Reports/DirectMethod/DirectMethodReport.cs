// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod
{
    using Microsoft.Azure.Devices.Edge.Util;
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
            : base(trackingId, resultType)
        {
            this.ExpectedSource = Preconditions.CheckNonWhiteSpace(expectedSource, nameof(expectedSource));
            this.ActualSource = Preconditions.CheckNonWhiteSpace(actualSource, nameof(actualSource));
            this.NetworkOnSuccess = networkOnSuccess;
            this.NetworkOffSuccess = networkOffSuccess;
            this.NetworkOnToleratedSuccess = networkOnToleratedSuccess;
            this.NetworkOffToleratedSuccess = networkOffToleratedSuccess;
            this.NetworkOnFailure = networkOnFailure;
            this.NetworkOffFailure = networkOffFailure;
            this.MismatchSuccess = mismatchSuccess;
            this.MismatchFailure = mismatchFailure;
        }

        public string ExpectedSource { get; }

        public string ActualSource { get; }

        // NetworkOnSuccess is when the network is online and the DirectMethod call succeeds.
        public ulong NetworkOnSuccess { get; }

        // NetworkOffSuccess is when the network is off and the DM call fails.
        public ulong NetworkOffSuccess { get; }

        // NetworkOnToleratedSuccess is when the network is on and the DM call fails, but it is within the tolerance period.
        public ulong NetworkOnToleratedSuccess { get; }

        // NetworkOffToleratedSuccess is when the network is off, and the DM call succeeds, but it is within the tolerance period.
        public ulong NetworkOffToleratedSuccess { get; }

        // NetworkOnFailure is when the network is on, but the DM call fails (not in tolerance period).
        public ulong NetworkOnFailure { get; }

        // NetworkOffFailure is when the network is off, but the DM call succeeds (not in tolerance period).
        public ulong NetworkOffFailure { get; }

        // MismatchSuccess is when the DM call succeeds, but there is no result reported from ActualStore (DMReceiver).
        public ulong MismatchSuccess { get; }

        // MismatchFailure is when the there is a result in ActualStore but no result in ExpectedStore. This should never happen.
        public ulong MismatchFailure { get; }

        public override string Title => $"DirectMethod Report ({this.ResultType}) for [{this.ExpectedSource}] and [{this.ActualSource}]";

        public override bool IsPassed =>
            this.MismatchFailure == 0 && this.NetworkOffFailure == 0 && this.NetworkOnFailure == 0;
    }
}
