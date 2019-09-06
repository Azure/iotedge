// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer
{
    using System;

    public class DirectMethodStatus
    {
        public string ModuleId { get; set; }

        public string StatusCode { get; set; }

        public string ResultAsJson { get; set; }

        public long SequenceNumber { get; set; }

        public DateTime EnqueuedDateTime { get; set; }
    }
}
