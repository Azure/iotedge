// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.DirectMethod.LongHaul
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json.Converters;

    class DirectMethodLongHaulReport : TestResultReportBase
    {
        public DirectMethodLongHaulReport(
            string testDescription,
            string trackingId,
            string senderSource,
            string receiverSource,
            string resultType,
            Topology topology,
            bool mqttBrokerEnabled,
            long senderSuccesses,
            long receiverSuccesses,
            long statusCodeZero,
            long unauthorized,
            long deviceNotFound,
            long transientError,
            long resourceError,
            long notImplemented,
            Dictionary<HttpStatusCode, long> other)
            : base(testDescription, trackingId, resultType)
        {
            this.SenderSource = Preconditions.CheckNonWhiteSpace(senderSource, nameof(senderSource));
            this.Topology = topology;
            this.MqttBrokerEnabled = mqttBrokerEnabled;
            this.ReceiverSource = receiverSource;
            this.SenderSuccesses = senderSuccesses;
            this.ReceiverSuccesses = receiverSuccesses;
            this.StatusCodeZero = statusCodeZero;
            this.Unauthorized = unauthorized;
            this.DeviceNotFound = deviceNotFound;
            this.TransientError = transientError;
            this.ResourceError = resourceError;
            this.NotImplemented = notImplemented;
            this.Other = other;
        }

        public string SenderSource { get; }

        [JsonConverter(typeof(StringEnumConverter))]
        public Topology Topology { get; }

        public bool MqttBrokerEnabled { get; }

        public string ReceiverSource { get; }

        public long SenderSuccesses { get; }

        public long ReceiverSuccesses { get; }

        public long StatusCodeZero { get; }

        public long Unauthorized { get; }

        public long DeviceNotFound { get; }

        public long TransientError { get; }

        public long ResourceError { get; }

        public long NotImplemented { get; }

        public Dictionary<HttpStatusCode, long> Other { get; }

        public override string Title => $"DirectMethod LongHaul Report for [{this.SenderSource}] and [{this.ReceiverSource}] ({this.ResultType})";

        public override bool IsPassed => this.IsPassedHelper();

        bool IsPassedHelper()
        {
            if (this.Other.Sum(x => x.Value) > 0)
            {
                // fail if we find anything that is not a 200 or 0 (most notably, 500's)
                return false;
            }

            bool senderAndReceiverSuccessesPass = this.SenderSuccesses <= this.ReceiverSuccesses;
            long allStatusCount = this.SenderSuccesses + this.StatusCodeZero + this.Unauthorized + this.DeviceNotFound + this.TransientError + this.ResourceError + this.NotImplemented + this.Other.Sum(x => x.Value);

            double statusCodeZeroThreshold;
            double unauthorizedThreshold;
            double deviceNotFoundThreshold;
            double transientErrorThreshold;
            double resourceErrorThreshold;
            double notImplementedThreshold;

            // The SDK does not allow edgehub to de-register from iothub subscriptions, which results in DirectMethod clients sometimes receiving status code 0.
            // Github issue: https://github.com/Azure/iotedge/issues/681
            // We expect to get this status sometimes because of edgehub restarts, but if we receive too many we should fail the tests.
            // TODO: When the SDK allows edgehub to de-register from subscriptions and we make the fix in edgehub, then we can fail tests for any status code 0.
            statusCodeZeroThreshold = (double)allStatusCount / 1000;

            // Sometimes transient network/resource errors are caught necessitating a tolerance.
            transientErrorThreshold = (double)allStatusCount / 1000;
            resourceErrorThreshold = (double)allStatusCount / 1000;

            // Sometimes iothub returns Unauthorized or NotImplemented that then later recovers.
            // Only occurs with broker enabled, so only apply tolerance in this case.
            if (this.MqttBrokerEnabled)
            {
                unauthorizedThreshold = (double)allStatusCount / 1000;
                notImplementedThreshold = (double)allStatusCount / 1000;
            }
            else
            {
                unauthorizedThreshold = (double)allStatusCount / double.MaxValue;
                notImplementedThreshold = (double)allStatusCount / double.MaxValue;
            }

            // DeviceNotFound typically happens when EdgeHub restarts and is offline.
            // For different test suites this happens at different rates.
            // 1) Single node runs arm devices, so this tolerance is a bit lenient.
            // 2) Nested non-broker has some product issue where we need some tolerance.
            // 3) Nested broker-enabled is the most stable.
            if (this.Topology == Topology.SingleNode && !this.MqttBrokerEnabled)
            {
                deviceNotFoundThreshold = (double)allStatusCount / 200;
            }
            else if (this.Topology == Topology.Nested && !this.MqttBrokerEnabled)
            {
                deviceNotFoundThreshold = (double)allStatusCount / 250;
            }
            else
            {
                deviceNotFoundThreshold = (double)allStatusCount / 400;
            }

            bool statusCodeZeroBelowThreshold = (this.StatusCodeZero == 0) || (this.StatusCodeZero < statusCodeZeroThreshold);
            bool unauthorizedBelowThreshold = (this.Unauthorized == 0) || (this.Unauthorized < unauthorizedThreshold);
            bool deviceNotFoundBelowThreshold = (this.DeviceNotFound == 0) || (this.DeviceNotFound < deviceNotFoundThreshold);
            bool transientErrorBelowThreshold = (this.TransientError == 0) || (this.TransientError < transientErrorThreshold);
            bool resourceErrorBelowThreshold = (this.ResourceError == 0) || (this.ResourceError < resourceErrorThreshold);
            bool notImplementedBelowThreshold = (this.NotImplemented == 0) || (this.NotImplemented < notImplementedThreshold);

            // Pass if below the thresholds, and sender and receiver got same amount of successess (or receiver has no results)
            return statusCodeZeroBelowThreshold && unauthorizedBelowThreshold && deviceNotFoundBelowThreshold && transientErrorBelowThreshold && senderAndReceiverSuccessesPass && notImplementedBelowThreshold;
        }
    }
}
