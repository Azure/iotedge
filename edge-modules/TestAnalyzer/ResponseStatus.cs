// Copyright (c) Microsoft. All rights reserved.
namespace TestAnalyzer
{
    using System;

    public class ResponseStatus
    {
        public string ModuleId { get; set; }

        public string StatusCode { get; set; }

        public string ResultAsJson { get; set; }

        public DateTime EnqueuedDateTime { get; set; }
    }
}
