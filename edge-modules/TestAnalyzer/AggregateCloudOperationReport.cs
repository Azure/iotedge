// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class AggregateCloudOperationReport
    {
        public AggregateCloudOperationReport(string moduleId, IDictionary<string, Tuple<int, DateTime>> statusCodes)
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
        }

        public string ModuleId { get; }

        public IList<CloudOperationReport> StatusCodes { get; }

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
    }
}
