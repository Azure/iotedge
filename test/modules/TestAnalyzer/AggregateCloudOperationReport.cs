// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class AggregateCloudOperationReport
    {
        public AggregateCloudOperationReport(string moduleId, IDictionary<string, Tuple<int, DateTime>> statusCodes, IDictionary<string, string> testInfo)
        {
            this.ModuleId = moduleId;
            this.StatusCodes = new List<CloudOperationReport>();
            foreach (KeyValuePair<string, Tuple<int, DateTime>> status in statusCodes)
            {
                this.StatusCodes.Add(new CloudOperationReport()
                {
                    StatusCode = status.Key,
                    Count = status.Value.Item1,
                    LastReceivedAt = status.Value.Item2
                });
            }

            this.TestInfo = testInfo;
        }

        public string ModuleId { get; }

        public IList<CloudOperationReport> StatusCodes { get; }

        public IDictionary<string, string> TestInfo { get; }

        public bool IsPassed => this.IsPassedHelper();

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
        public class CloudOperationReport
        {
            public string StatusCode { get; set; }

            public int Count { get; set; }

            public DateTime LastReceivedAt { get; set; }
        }

        bool IsPassedHelper()
        {
            bool noUnexpectedStatus = this.StatusCodes.All(
                x => x.StatusCode.StartsWith("200") ||
                x.StatusCode.Equals("OK") ||
                (x.StatusCode.Equals("0") && this.ModuleId.Contains("DirectMethod")));

            // The SDK does not allow edgehub to de-register from iothub subscriptions, which results in DirectMethod clients sometimes receiving status code 0.
            // Github issue: https://github.com/Azure/iotedge/issues/681
            // We expect to get this status sometimes because of edgehub restarts, but if we receive too many we should fail the tests.
            // TODO: When the SDK allows edgehub to de-register from subscriptions and we make the fix in edgehub, then we can fail tests for any status code 0.
            bool statusCodeZeroBelowThreshold = this.StatusCodes.Where(s => s.StatusCode.Equals("0")).Count() < ((double)this.StatusCodes.Count / 100);

            return noUnexpectedStatus && statusCodeZeroBelowThreshold;
        }
    }
}
