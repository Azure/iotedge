// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using System.Net;

    public class EdgeHubRestartTestResult : TestResultBase
    {
        public EdgeHubRestartTestResult(
            string source,
            TestOperationResultType testOperationResultType,
            DateTime createdAt)
            : base(source, testOperationResultType, createdAt)
        {
        }

        public TestResultBase TestResult;

        public DateTime EdgeHubRestartTime { get; set; }

        public DateTime EdgeHubUplinkTime { get; set; }

        public HttpStatusCode RestartHttpStatusCode { get; set; }
        
        public HttpStatusCode UplinkHttpStatusCode { get; set; }

        public override string GetFormattedResult()
        {
            return this.TestResult.GetFormattedResult();
        }
    }
}