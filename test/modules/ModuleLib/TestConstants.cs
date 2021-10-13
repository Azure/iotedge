// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil
{
    public static class TestConstants
    {
        public static class Message
        {
            public const string TrackingIdPropertyName = "trackingId";
            public const string BatchIdPropertyName = "batchId";
            public const string SequenceNumberPropertyName = "sequenceNumber";
            public const string TraceIdPropertyName = "traceId";
            public const string SpanIdPropertyName = "spanId";
        }

        public static class Error
        {
            public const string TestResultSource = "error";
        }

        public static class TestInfo
        {
            public const string TestResultSource = "testinfo";
        }

        public static class NetworkController
        {
            public const string RunProfilePropertyName = "NetworkControllerRunProfile";
        }
    }
}
