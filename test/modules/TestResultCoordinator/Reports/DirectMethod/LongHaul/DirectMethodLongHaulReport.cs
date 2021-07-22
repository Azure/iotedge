// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod.LongHaul
{
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
            string receiverSource,
            string resultType,
            long senderSuccesses,
            long receiverSuccesses,
            long statusCodeZero,
            long deviceNotFound,
            long transientError,
            long resourceError,
            Dictionary<HttpStatusCode, long> other)
            : base(testDescription, trackingId, resultType)
        {
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.ReceiverSource = receiverSource;
            this.SenderSuccesses = senderSuccesses;
            this.ReceiverSuccesses = receiverSuccesses;
            this.StatusCodeZero = statusCodeZero;
            this.DeviceNotFound = deviceNotFound;
            this.TransientError = transientError;
            this.ResourceError = resourceError;
            this.Other = other;
        }

        public string SenderSource { get; }
        public string ReceiverSource { get; }
        public long SenderSuccesses { get; }
        public long ReceiverSuccesses { get; }
        public long StatusCodeZero { get; }
        public long DeviceNotFound { get; }
        public long TransientError { get; }
        public long ResourceError { get; }
        public Dictionary<HttpStatusCode, long> Other { get; }

        public override string Title => $"DirectMethod LongHaul Report for [{this.SenderSource}] and [{this.ReceiverSource}] ({this.ResultType})";

        public override bool IsPassed => this.IsPassedHelper();

        public bool IsPassedHelper()
        {
            if (this.Other.Sum(x => x.Value) > 0)
            {
                // fail if we find anything that is not a 200 or 0 (most notably, 500's)
                return false;
            }

            bool senderAndReceiverSuccessesPass = this.SenderSuccesses <= this.ReceiverSuccesses;

            // The SDK does not allow edgehub to de-register from iothub subscriptions, which results in DirectMethod clients sometimes receiving status code 0.
            // Github issue: https://github.com/Azure/iotedge/issues/681
            // We expect to get this status sometimes because of edgehub restarts, but if we receive too many we should fail the tests.
            // TODO: When the SDK allows edgehub to de-register from subscriptions and we make the fix in edgehub, then we can fail tests for any status code 0.
            long allStatusCount = this.SenderSuccesses + this.StatusCodeZero + this.Other.Sum(x => x.Value);
            bool statusCodeZeroBelowThreshold = (this.StatusCodeZero == 0) || (this.StatusCodeZero < ((double)allStatusCount / 1000));
            bool deviceNotFoundBelowThreshold = (this.DeviceNotFound == 0) || (this.DeviceNotFound < ((double)allStatusCount / 100));
            bool transientErrorBelowThreshold = (this.TransientError == 0) || (this.TransientError < ((double)allStatusCount / 100));
            bool resourceErrorBelowThreshold = (this.ResourceError == 0) || (this.ResourceError < ((double)allStatusCount / 100));

            // Pass if below the thresholds, and sender and receiver got same amount of successess (or receiver has no results)
            return statusCodeZeroBelowThreshold && deviceNotFoundBelowThreshold && transientErrorBelowThreshold && senderAndReceiverSuccessesPass;
        }
    }
}
