// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class ResponseOrientedReport
    {
        public ResponseOrientedReport(string moduleId, IDictionary<string, Tuple<int, DateTime>> statusCodes)
        {
            this.ModuleId = moduleId;
            this.StatusCodes = new List<ResponseStatusReport>();
            foreach (KeyValuePair<string, Tuple<int, DateTime>> status in statusCodes)
            {
                this.StatusCodes.Add(new ResponseStatusReport()
                {
                    StatusCode = status.Key,
                    Count = status.Value.Item1,
                    LastReceivedAt = status.Value.Item2
                });
            }
        }

        public string ModuleId { get; }

        public IList<ResponseStatusReport> StatusCodes { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
        public class ResponseStatusReport
        {
            public string StatusCode { get; set; }

            public int Count { get; set; }

            public DateTime LastReceivedAt { get; set; }
        }
    }
}
