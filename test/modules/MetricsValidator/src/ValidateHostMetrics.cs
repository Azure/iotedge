// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;

    public class ValidateHostMetrics
    {
        TestReporter testReporter;
        IMetricsScraper scraper;

        public ValidateHostMetrics(TestReporter testReporter, IMetricsScraper scraper)
        {
            this.testReporter = testReporter.MakeSubcategory(nameof(ValidateHostMetrics));
            this.scraper = scraper;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);
            var names = metrics.Select(m => m.Name);


        }
    }
}
