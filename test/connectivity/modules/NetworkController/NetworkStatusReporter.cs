// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResultCoordinatorClient;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using ModuleUtil.NetworkControllerResult;

    class NetworkStatusReporter : INetworkStatusReporter
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<NetworkStatusReporter>();
        readonly TestResultCoordinatorClient trcClient;
        readonly string moduleId;
        readonly string trackingId;

        public NetworkStatusReporter(Uri testResultCoordinatorEndpoint, string moduleId, string trackingId)
        {
            this.trcClient = new TestResultCoordinatorClient() { BaseUrl = testResultCoordinatorEndpoint.AbsoluteUri };
            this.moduleId = moduleId;
            this.trackingId = trackingId;
        }

        public Task ReportNetworkStatus(NetworkControllerOperation operation, bool enabled, NetworkStatus networkStatus, bool success = true)
        {
            var networkController = new NetworkControllerResult() { Operation = operation.ToString(), OperationStatus = success ? "Success" : "Failed", NetworkStatus = networkStatus, Enabled = enabled, TrackingId = this.trackingId };
            return ModuleUtil.ReportStatus(this.trcClient, Log, this.moduleId, networkController.ToString(), TestOperationResultType.Network.ToString());
        }
    }
}
