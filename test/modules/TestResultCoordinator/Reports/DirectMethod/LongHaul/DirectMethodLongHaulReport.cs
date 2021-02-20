// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod.LongHaul
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    class DirectMethodLongHaulReport : TestResultReportBase
    {
        public DirectMethodLongHaulReport(
            string testDescription,
            string trackingId,
            string senderSource,
            string receiverSource,
            string resultType,
            ulong senderSuccesses,
            ulong receiverSuccesses,
            ulong statusCodeZero,
            ulong unknown)
            : base(testDescription, trackingId, resultType)
        {
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = receiverSource;
            this.SenderSuccesses = senderSuccesses;
            this.ReceiverSuccesses = receiverSuccesses;
            this.StatusCodeZero = statusCodeZero;
            this.Unknown = unknown;
        }

        public string SenderSource { get; }
        public string ReceiverSource { get; }
        public ulong SenderSuccesses { get; }
        public ulong ReceiverSuccesses { get; }
        public ulong StatusCodeZero { get; }
        public ulong Unknown { get; }

        public override string Title => $"DirectMethod LongHaul Report for [{this.SenderSource}] and [{this.ReceiverSource}] ({this.ResultType})";

        public override bool IsPassed => this.IsPassedHelper();

        public bool IsPassedHelper()
        {
            if (this.Unknown > 0)
            {
                // fail if we find any unknowns (anything that's not a 200 or a 0 - most notably, 500's)
                return false;
            }

            bool senderAndReceiverSuccessesPass = this.SenderSuccesses == this.ReceiverSuccesses;
            // The SDK does not allow edgehub to de-register from iothub subscriptions, which results in DirectMethod clients sometimes receiving status code 0.
            // Github issue: https://github.com/Azure/iotedge/issues/681
            // We expect to get this status sometimes because of edgehub restarts, but if we receive too many we should fail the tests.
            // TODO: When the SDK allows edgehub to de-register from subscriptions and we make the fix in edgehub, then we can fail tests for any status code 0.
            ulong allStatusCount = this.SenderSuccesses + this.StatusCodeZero + this.Unknown;
            bool statusCodeZeroBelowThreshold = (this.StatusCodeZero == 0) || (this.StatusCodeZero < ((double)allStatusCount / 100));

            // Pass if status code zero is below the threshold, and sender and receiver got same amount of successess (or receiver has no results)
            return statusCodeZeroBelowThreshold && senderAndReceiverSuccessesPass;
        }
    }
}
