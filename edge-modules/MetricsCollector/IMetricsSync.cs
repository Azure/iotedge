// Copyright (c) Microsoft. All rights reserved.
namespace MetricsCollector
{
    using System.Threading.Tasks;

    interface IMetricsSync
    {
        Task ScrapeAndSyncMetricsAsync();
    }
}
