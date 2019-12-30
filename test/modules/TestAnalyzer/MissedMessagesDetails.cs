// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    class MissedMessagesDetails
    {
        public MissedMessagesDetails(long missedMessagesCount, DateTime startDateTime, DateTime endDateTime)
        {
            this.MissedMessagesCount = missedMessagesCount;
            this.StartDateTime = startDateTime;
            this.EndDateTime = endDateTime;
        }

        public long MissedMessagesCount { get; }

        public DateTime StartDateTime { get; }

        public DateTime EndDateTime { get; }
    }
}
