// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    using System;
    using Microsoft.Azure.WebJobs.Description;

    [Binding]
    [AttributeUsage(AttributeTargets.Parameter)]
    public class EdgeHubAttribute : Attribute
    {
        public string OutputName { get; set; }

        public int BatchSize { get; set; }
    }
}
