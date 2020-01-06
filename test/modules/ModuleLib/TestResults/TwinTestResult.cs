// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil
{
    using System;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    public class TwinTestResult : TestResultBase
    {
        public TwinTestResult(string source, DateTime createdAt) :
            base(source, TestOperationResultType.Twin, createdAt)
        {
        }

        public string Operation { get; set; }

        public string TrackingId { get; set; }

        public TwinCollection Properties { get; set; }

        public string ErrorMessage { get; set; }

        public override string GetFormattedResult()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
