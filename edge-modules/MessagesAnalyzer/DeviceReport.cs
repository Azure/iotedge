// Copyright (c) Microsoft. All rights reserved.

namespace MessagesAnalyzer
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class DeviceReport
    {
        public DeviceReport(IList<ModuleReport> report)
        {
            this.Report = report;
        }

        IList<ModuleReport> Report { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this.Report, Formatting.Indented);
        }
    }
}
