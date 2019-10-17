// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class DeviceAnalysis
    {
        public DeviceAnalysis(IList<ModuleMessagesReport> messagesReport, IList<ModuleDmReport> dmReport)
        {
            this.MessagesReport = messagesReport;
            this.DmReport = dmReport;
        }

        public IList<ModuleDmReport> DmReport { get; }

        public IList<ModuleMessagesReport> MessagesReport { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
