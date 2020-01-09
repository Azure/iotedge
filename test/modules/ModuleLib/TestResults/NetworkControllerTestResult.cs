// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;
    using Newtonsoft.Json;

    public class NetworkControllerTestResult : TestResultBase
    {
        public NetworkControllerTestResult(string source, DateTime createdAt)
            : base(source, TestOperationResultType.Network, createdAt)
        {
        }

        public string TrackingId { get; set; }

        public string Operation { get; set; }

        public string OperationStatus { get; set; }

        public NetworkControllerType NetworkControllerType { get; set; }

        public NetworkControllerStatus NetworkControllerStatus { get; set; }

        public override string GetFormattedResult()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
