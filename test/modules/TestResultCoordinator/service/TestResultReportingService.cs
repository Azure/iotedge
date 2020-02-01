// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Service
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using TestResultCoordinator.Report;
    using TestResultCoordinator.Storage;

    // This class implementation is copied from https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-3.1&tabs=visual-studio
    // And then implement our own DoWorkAsync for reporting generation.
    class TestResultReportingService : IHostedService, IDisposable
    {
        readonly ILogger logger = ModuleUtil.CreateLogger(nameof(TestResultReportingService));
        readonly TimeSpan delayBeforeWork;
        readonly ITestOperationResultStorage storage;
        Timer timer;

        public TestResultReportingService(ITestOperationResultStorage storage)
        {
            this.storage = Preconditions.CheckNotNull(storage, nameof(storage));
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
            ITestResultReport[] testResultReports = await TestReportHelper.GenerateTestResultReports(this.storage, this.logger);

            if (testResultReports.Length == 0)
            {
                this.logger.LogInformation("No test result report is generated.");
                return;
            }

            string reportsContent = JsonConvert.SerializeObject(testResultReports, Formatting.Indented);
            this.logger.LogInformation($"Test result report{Environment.NewLine}{reportsContent}");

            await AzureLogAnalytics.Instance.PostAsync(
                Settings.Current.LogAnalyticsWorkspaceId,
                Settings.Current.LogAnalyticsSharedKey,
                reportsContent,
                Settings.Current.LogAnalyticsLogType);

            this.logger.LogInformation("Successfully send reports to LogAnalytics");
        }
    }
}
