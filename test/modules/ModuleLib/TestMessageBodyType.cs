// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil
{
    using System;
    using Microsoft.Azure.Devices.Client;

    public class TestMessageBodyType
    {
        public string ModuleId { get; set; }
        public string OutputName { get; set; }
        public DateTime TimeCreated { get; set; } 
        public DateTime TimeLatestRelayed { get; set; }
        public string TrackingId { get; set; }
        public TransportType TransportType { get; set; }
        public int MessageSizeInBytes { get; set; }
        public byte Nounce { get; set; }
    }
}
