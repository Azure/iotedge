// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Azure.WebJobs.Extensions.EdgeHub;
using Microsoft.Azure.WebJobs.Hosting;

[assembly: WebJobsStartup(typeof(EdgeHubWebJobsStartup))]

namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    using Microsoft.Azure.WebJobs.Extensions.EdgeHub.Config;
    using Microsoft.Azure.WebJobs.Hosting;

    public class EdgeHubWebJobsStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder) => builder.AddEdge();
    }
}
