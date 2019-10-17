// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class ModuleDmReport
    {
        public ModuleDmReport(string moduleId, IDictionary<string, Tuple<int, DateTime>> statusCodes)
        {
            this.ModuleId = moduleId;
            this.StatusCodes = new List<DmStatusReport>();
            foreach (KeyValuePair<string, Tuple<int, DateTime>> status in statusCodes)
            {
                this.StatusCodes.Add(new DmStatusReport()
                {
                    StatusCode = status.Key,
                    Count = status.Value.Item1,
                    LastReceivedAt = status.Value.Item2
                });
            } 
        }

        public string ModuleId { get; }

        public IList<DmStatusReport> StatusCodes { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
        public class DmStatusReport
        {
            public string StatusCode { get; set; }

            public int Count { get; set; }

            public DateTime LastReceivedAt { get; set; }
        }
    }
}
