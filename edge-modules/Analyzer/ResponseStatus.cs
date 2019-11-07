// Copyright (c) Microsoft. All rights reserved.
namespace Analyzer
{
    using System;

    public class ResponseStatus
    {
        public string ModuleId { get; set; }

        public string StatusCode { get; set; }

        public string ResultAsJson { get; set; }

        // TODO: consider removing
        public long SequenceNumber { get; set; }

        public DateTime EnqueuedDateTime { get; set; }
    }
}
