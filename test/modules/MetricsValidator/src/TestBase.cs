// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
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

        protected string TestName
        {
            get { return this.GetType().ToString().Split('.').Last(); }
        }

        public TestBase(TestReporter testReporter, IMetricsScraper scraper, ModuleClient moduleClient)
        {
            log.LogInformation($"Making test {this.TestName}");
            this.testReporter = testReporter.MakeSubcategory(this.TestName);
            this.scraper = scraper;
            this.moduleClient = moduleClient;
        }

        public async Task Start(CancellationToken cancellationToken)
        {
            log.LogInformation($"Starting test {this.TestName}");

            using (this.testReporter.MeasureDuration())
            {
                try
                {
                    await this.Test(cancellationToken);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, $"{this.TestName} Failed");
                    this.testReporter.Assert("Test doesn't break", false, $"Test threw exception:\n{ex}");
                }
            }

            log.LogInformation($"Finished test {this.TestName} in {this.testReporter.Duration}.");
        }

        protected abstract Task Test(CancellationToken cancellationToken);
    }
}
