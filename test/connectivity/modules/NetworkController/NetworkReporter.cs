// Copyright (c) Microsoft. All rights reserved.
namespace NetworkController
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResultCoordinatorClient;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    class NetworkReporter : INetworkStatusReporter
    {
        static readonly ILogger Log = Logger.Factory.CreateLogger<NetworkReporter>();
        readonly TestResultCoordinatorClient trcClient;
        readonly string moduleId;
        readonly string trackingId;

        public NetworkReporter(Uri testResultCoordinatorEndpoint, string moduleId, string trackingId)
        {
            this.trcClient = new TestResultCoordinatorClient() { BaseUrl = testResultCoordinatorEndpoint.AbsoluteUri };
            this.moduleId = moduleId;
            this.trackingId = trackingId;
        }

        public Task ReportNetworkStatus(NetworkControllerOperation operation, NetworkStatus status, string description, bool success = true)
        {
            NetworkControllerResult networkController = new NetworkControllerResult() { Operation = operation.ToString(), OperationStatus = success ? "Success" : "Failed", Description = description, NetworkStatus = status.ToString(), TrackingId = this.trackingId };
            return ModuleUtil.ReportStatus(this.trcClient, Log, this.moduleId, networkController.ToString(), TestOperationResultType.Network.ToString());
        }

        class NetworkControllerResult
        {
            public string TrackingId { get; set; }

            public string Operation { get; set; }

            public string OperationStatus { get; set; }

            public string Description { get; set; }

            public string NetworkStatus { get; set; }

            public override string ToString()
            {
                return JsonConvert.SerializeObject(this, Formatting.Indented);
            }
        }
    }
}
