// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class ModuleMessagesReport
    {
        public ModuleMessagesReport(string moduleId, StatusCode statusCode, long receivedMessagesCount, string statusMessage, IDictionary<string, string> testInfo)
            : this(moduleId, statusCode, receivedMessagesCount, statusMessage, DateTime.MinValue, new List<MissedMessagesDetails>(), testInfo)
        {
        }

        public ModuleMessagesReport(string moduleId, StatusCode statusCode, long receivedMessagesCount, string statusMessage, DateTime lastMessageReceivedAt, IList<MissedMessagesDetails> missedMessages, IDictionary<string, string> testInfo)
        {
            this.ModuleId = moduleId;
            this.StatusCode = statusCode;
            this.ReceivedMessagesCount = receivedMessagesCount;
            this.StatusMessage = statusMessage;
            this.MissedMessages = missedMessages;
            this.LastMessageReceivedAt = lastMessageReceivedAt;
            this.TestInfo = testInfo;
        }

        public string ModuleId { get; }

        public StatusCode StatusCode { get; }

        public string StatusMessage { get; }

        public long ReceivedMessagesCount { get; }

        public DateTime LastMessageReceivedAt { get; }

        public IList<MissedMessagesDetails> MissedMessages { get; }

        public bool IsPassed => this.StatusCode == StatusCode.AllMessages || this.StatusCode == StatusCode.OldMessages;

        public IDictionary<string, string> TestInfo { get; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
