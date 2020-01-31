// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using Microsoft.Azure.Devices.Client;

    public class CloudToDeviceMessageTesterMetadata
    {
        public struct SharedMetadata
        {
            public string IotHubConnectionString;
            public string DeviceId;
            public string ModuleId;
        }

        public struct ReceiverMetadata
        {
            public TransportType TransportType;
            public string GatewayHostName;
            public string WorkloadUri;
            public string ApiVersion;
            public string ModuleGenerationId;
            public string IotHubHostName;
        }

        public struct SenderMetadata
        {
            public string TrackingId;
            public TimeSpan MessageDelay;
            public TimeSpan TestStartDelay;
            public TimeSpan TestDuration;
        }
    }
}
