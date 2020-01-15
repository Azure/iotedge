// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public abstract class TestBase
    {
        protected static ILogger log = Logger.Factory.CreateLogger<TestBase>();

        protected TestReporter testReporter;
        protected IMetricsScraper scraper;
        protected ModuleClient moduleClient;

        public TestBase(TestReporter testReporter, IMetricsScraper scraper, ModuleClient moduleClient)
        {
            log.LogInformation($"Making test {this.GetType().ToString()}");
            this.testReporter = testReporter.MakeSubcategory(this.GetType().ToString());
            this.scraper = scraper;
            this.moduleClient = moduleClient;
        }

        public abstract Task Start(CancellationToken cancellationToken);
    }
}
