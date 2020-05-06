// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class TestResultAnalysis
    {
        public TestResultAnalysis(IList<ModuleMessagesReport> messagesReport, IList<AggregateCloudOperationReport> dmReport, IList<AggregateCloudOperationReport> twinsReport)
        {
            this.MessagesReport = messagesReport;
            this.DmReport = dmReport;
            this.TwinsReport = twinsReport;
        }

        public IList<AggregateCloudOperationReport> DmReport { get; }
        public IList<AggregateCloudOperationReport> TwinsReport { get; }
        public IList<ModuleMessagesReport> MessagesReport { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
