// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults
{
    using System;
    using System.Net;
    using Newtonsoft.Json;

    public class EdgeHubRestartTestResult : TestResultBase
    {
        public EdgeHubRestartTestResult(
            string source,
            TestOperationResultType testOperationResultType,
            DateTime createdAt)
            : base(source, testOperationResultType, createdAt)
        {
        }

        public TestResultBase TestResult
        {
            get
            {
                return this.TestResult;
            }
            
            set
            {
                this.TestResult = value;
                this.TestResultType = this.TestResult.GetType().ToString();
            }
        }

        public string TestResultType { get; private set; }

        public DateTime EdgeHubRestartTime { get; set; }

        public DateTime EdgeHubUplinkTime { get; set; }

        public HttpStatusCode RestartHttpStatusCode { get; set; }
        
        public HttpStatusCode UplinkHttpStatusCode { get; set; }

        public override string GetFormattedResult()
        {
            return JsonConvert.SerializeObject(this);
        }

        public string GetAttachedTestResult()
        {
            return this.TestResult.GetFormattedResult();
        }
    }
}