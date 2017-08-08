// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Functions.Binding
{
    using System;
    using Microsoft.Azure.WebJobs.Description;

    [Binding]
    public class EdgeHubAttribute : Attribute
    {
        public string OutputName { get; set; }

        public int BatchSize { get; set; }
    }
}
