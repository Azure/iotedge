// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;
    using Newtonsoft.Json;

    public class MessageDetails
    {
        [JsonConstructor]
        public MessageDetails(string moduleId, string batchId, long sequenceNumber, DateTime enqueuedDateTime)
        {
            this.SequenceNumber = sequenceNumber;
            this.EnqueuedDateTime = enqueuedDateTime;
            this.ModuleId = moduleId;
            this.BatchId = batchId;
        }

        public long SequenceNumber { get; }

        public DateTime EnqueuedDateTime { get; }

        public string ModuleId { get; }

        public string BatchId { get; }
    }
}
