// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod.LongHaul
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util;

    class DirectMethodLongHaulReport : TestResultReportBase
    {
        public DirectMethodLongHaulReport(
            string testDescription,
            string trackingId,
            string senderSource,
            Option<string> receiverSource,
            string resultType,
            long senderSuccesses,
            Option<long> receiverSuccesses,
            long statusCodeZero,
            Dictionary<HttpStatusCode, long> other)
            : base(testDescription, trackingId, resultType)
        {
            if (receiverSource.HasValue ^ receiverSuccesses.HasValue)
            {
                throw new ArgumentException("Provide both receiverSource and receiverSuccesses or neither.");
            }

            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = receiverSource;
            this.SenderSuccesses = senderSuccesses;
            this.ReceiverSuccesses = receiverSuccesses;
            this.StatusCodeZero = statusCodeZero;
            this.Other = other;
        }

        public string SenderSource { get; }
        public Option<string> ReceiverSource { get; }
        public long SenderSuccesses { get; }
        public Option<long> ReceiverSuccesses { get; }
        public long StatusCodeZero { get; }
        public Dictionary<HttpStatusCode, long> Other { get; }

        public override string Title => this.ReceiverSource.HasValue ?
            $"DirectMethod LongHaul Report for [{this.SenderSource}] and [{this.ReceiverSource.OrDefault()}] ({this.ResultType})" : $"DirectMethod Report for [{this.SenderSource}] ({this.ResultType})";

        public override bool IsPassed => this.IsPassedHelper();

        public bool IsPassedHelper()
        {
            if (this.Other.Sum(x => x.Value) > 0)
            {
                // fail if we find anything that is not a 200 or 0 (most notably, 500's)
                return false;
            }

            bool senderAndReceiverSuccessesPass = this.ReceiverSuccesses.Match(r => this.SenderSuccesses == r, () => true);
            // The SDK does not allow edgehub to de-register from iothub subscriptions, which results in DirectMethod clients sometimes receiving status code 0.
            // Github issue: https://github.com/Azure/iotedge/issues/681
            // We expect to get this status sometimes because of edgehub restarts, but if we receive too many we should fail the tests.
            // TODO: When the SDK allows edgehub to de-register from subscriptions and we make the fix in edgehub, then we can fail tests for any status code 0.
            long allStatusCount = this.SenderSuccesses + this.StatusCodeZero + this.Other.Sum(x => x.Value);
            bool statusCodeZeroBelowThreshold = (this.StatusCodeZero == 0) || (this.StatusCodeZero < ((double)allStatusCount / 100));

            // Pass if status code zero is below the threshold, and sender and receiver got same amount of successess (or receiver has no results)
            return statusCodeZeroBelowThreshold && senderAndReceiverSuccessesPass;
        }
    }
}
