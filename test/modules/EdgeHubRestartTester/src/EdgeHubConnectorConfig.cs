// Copyright (c) Microsoft. All rights reserved.
namespace EdgeHubRestartTester
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util;

    class EdgeHubConnectorsConfig
    {
        public EdgeHubConnectorsConfig(
            TransportType transportType,
            string directMethodTargetModuleId,
            string messageOutputEndpoint)
        {
            this.TransportType = transportType;
            this.DirectMethodTargetModuleId = Preconditions.CheckNonWhiteSpace(directMethodTargetModuleId, nameof(directMethodTargetModuleId));
            this.MessageOutputEndpoint = Preconditions.CheckNonWhiteSpace(messageOutputEndpoint, nameof(messageOutputEndpoint));
        }

        public TransportType TransportType { get; }
        public string DirectMethodTargetModuleId { get; }
        public string MessageOutputEndpoint { get; }
    }
}