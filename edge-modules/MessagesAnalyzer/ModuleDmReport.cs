// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class ModuleDmReport
    {
        public ModuleDmReport(string moduleId, IDictionary<string, IList<DirectMethodStatus>> statusCodes)
        {
            this.ModuleId = moduleId;
            this.StatusCodes = new List<DmStatusReport>();
            foreach (KeyValuePair<string, IList<DirectMethodStatus>> status in statusCodes)
            {
                this.StatusCodes.Add(new DmStatusReport()
                {
                    StatusCode = status.Key,
                    Count = status.Value.Count,
                    LastReceivedAt = status.Value.Last().EnqueuedDateTime
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
