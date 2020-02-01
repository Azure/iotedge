// Copyright (c) Microsoft. All rights reserved.
namespace CloudToDeviceMessageTester
{
    using System;
    using Microsoft.Azure.Devices.Client;

    public struct C2DTestSharedSettings
    {
        public string IotHubConnectionString;
        public string DeviceId;
        public string ModuleId;
    }

    public struct C2DTestReceiverSettings
    {
        public TransportType TransportType;
        public string GatewayHostName;
        public string WorkloadUri;
        public string ApiVersion;
        public string ModuleGenerationId;
        public string IotHubHostName;
    }

    public struct C2DTestSenderSettings
    {
        public string TrackingId;
        public TimeSpan MessageDelay;
        public TimeSpan TestStartDelay;
        public TimeSpan TestDuration;
    }
}
