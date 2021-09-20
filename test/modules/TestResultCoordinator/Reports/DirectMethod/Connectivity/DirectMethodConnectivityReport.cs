// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod.Connectivity
{
    using Microsoft.Azure.Devices.Edge.Util;
    using TestResultCoordinator.Reports;

    /// <summary>
    /// This is a DirectMethod report to show test results for DirectMethods.
    /// </summary>
    class DirectMethodConnectivityReport : TestResultReportBase
    {
        public DirectMethodConnectivityReport(
            string testDescription,
            Topology topology,
            string trackingId,
            string senderSource,
            Option<string> receiverSource,
            string resultType,
            ulong networkOnSuccess,
            ulong networkOffSuccess,
            ulong networkOnToleratedSuccess,
            ulong networkOffToleratedSuccess,
            ulong networkOnFailure,
            ulong networkOffFailure,
            ulong mismatchSuccess,
            ulong mismatchFailure)
            : base(testDescription, trackingId, resultType)
        {
            this.Topology = topology;
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = receiverSource;
            this.NetworkOnSuccess = networkOnSuccess;
            this.NetworkOffSuccess = networkOffSuccess;
            this.NetworkOnToleratedSuccess = networkOnToleratedSuccess;
            this.NetworkOffToleratedSuccess = networkOffToleratedSuccess;
            this.NetworkOnFailure = networkOnFailure;
            this.NetworkOffFailure = networkOffFailure;
            this.MismatchSuccess = mismatchSuccess;
            this.MismatchFailure = mismatchFailure;
        }

        public Topology Topology { get; }

        public string SenderSource { get; }

        public Option<string> ReceiverSource { get; }

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

        public override string Title => this.ReceiverSource.HasValue ?
            $"DirectMethod Connectivity Report for [{this.SenderSource}] and [{this.ReceiverSource.OrDefault()}] ({this.ResultType})" : $"DirectMethod Report for [{this.SenderSource}] ({this.ResultType})";

        public override bool IsPassed => this.IsPassedHelper();

        public bool IsPassedHelper()
        {
            ulong totalSuccessful = this.NetworkOnSuccess + this.NetworkOffSuccess + this.NetworkOnToleratedSuccess + this.NetworkOffToleratedSuccess;
            ulong totalFailing = this.NetworkOffFailure + this.NetworkOnFailure;
            ulong totalResults = totalSuccessful + totalFailing;

            if (totalResults == 0)
            {
                return false;
            }
            else if (this.Topology == Topology.Nested)
            {
                // This tolerance is needed because sometimes we see large numbers of NetworkOnFailures.
                // Also, sometimes we observe 1 NetworkOffFailure and a lot of mismatched results. The
                // mismatched results are likely a test logic issue that needs further investigation.
                return totalSuccessful > 1;
            }
            else
            {
                // This tolerance is needed because sometimes we see large numbers of NetworkOnFailures.
                // When this product issue is resolved, we can remove this failure tolerance.
                bool areNetworkOnFailuresWithinThreshold = ((double)this.NetworkOnFailure / totalResults) < .30d;
                return this.MismatchFailure == 0 && this.NetworkOffFailure == 0 && areNetworkOnFailuresWithinThreshold && (this.NetworkOnSuccess + this.NetworkOffSuccess + this.NetworkOnToleratedSuccess + this.NetworkOffToleratedSuccess > 0);
            }
        }
    }
}
