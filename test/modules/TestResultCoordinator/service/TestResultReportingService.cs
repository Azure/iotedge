// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Service
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using TestResultCoordinator.Report;

    // This class implementation is copied from https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-3.1&tabs=visual-studio
    // And then implement our own DoWorkAsync for reporting generation.
    class TestResultReportingService : IHostedService, IDisposable
    {
        readonly ILogger logger = ModuleUtil.CreateLogger(nameof(TestResultReportingService));
        readonly TimeSpan delayBeforeWork;
        Timer timer;

        public TestResultReportingService()
        {
            this.delayBeforeWork = Settings.Current.TestStartDelay + Settings.Current.TestDuration + Settings.Current.DurationBeforeVerification;
        }

        public Task StartAsync(CancellationToken ct)
        {
            this.logger.LogInformation("Test Result Reporting Service running.");

            // Specify -1 ms to disable periodic run
            this.timer = new Timer(
                this.DoWorkAsync,
                null,
                this.delayBeforeWork,
                TimeSpan.FromMilliseconds(-1));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("Test Result Reporting Service is stopping.");

            this.timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            this.timer?.Dispose();
        }

        async void DoWorkAsync(object state)
        {
            this.logger.LogInformation($"Starting report generation for {Settings.Current.ReportMetadataList.Count} reports");

            try
            {
                var testReportGeneratorFactory = new TestReportGeneratorFactory();
                var testResultReportList = new List<Task<ITestResultReport>>();
                foreach (IReportMetadata reportMetadata in Settings.Current.ReportMetadataList)
                {
                    ITestResultReportGenerator testResultReportGenerator = testReportGeneratorFactory.Create(Settings.Current.TrackingId, reportMetadata);
                    testResultReportList.Add(testResultReportGenerator.CreateReportAsync());
                }

                ITestResultReport[] testResultReports = await Task.WhenAll(testResultReportList);

                this.logger.LogInformation("Successfully generated all reports");

                string reportsContent = JsonConvert.SerializeObject(testResultReports);
                this.logger.LogInformation(reportsContent);

                await AzureLogAnalytics.Instance.PostAsync(
                    Settings.Current.LogAnalyticsWorkspaceId,
                    Settings.Current.LogAnalyticsSharedKey,
                    reportsContent,
                    Settings.Current.LogAnalyticsLogType);

                this.logger.LogInformation("Successfully send reports to LogAnalytics");
            }
            catch (Exception ex)
            {
                this.logger.LogError("TestResultCoordinator failed during report generation", ex);
            }
        }
    }
}
