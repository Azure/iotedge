// Copyright (c) Microsoft. All rights reserved.
namespace Analyzer
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class DeviceAnalysis
    {
        public DeviceAnalysis(IList<ModuleMessagesReport> messagesReport, IList<ResponseOrientedReport> dmReport, IList<ResponseOrientedReport> twinsReport)
        {
            this.MessagesReport = messagesReport;
            this.DmReport = dmReport;
            this.TwinsReport = twinsReport;
        }

        public IList<ResponseOrientedReport> DmReport { get; }
        public IList<ResponseOrientedReport> TwinsReport { get; }
        public IList<ModuleMessagesReport> MessagesReport { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
