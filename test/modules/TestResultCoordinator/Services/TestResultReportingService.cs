// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using TestResultCoordinator.Reports;
    using TestResultCoordinator.Storage;

    // This class implementation is copied from https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-3.1&tabs=visual-studio
    // And then implement our own DoWorkAsync for reporting generation.
    class TestResultReportingService : IHostedService, IDisposable
    {
        readonly ILogger logger = ModuleUtil.CreateLogger(nameof(TestResultReportingService));
        readonly TimeSpan delayBeforeWork;
        readonly ITestOperationResultStorage storage;
        readonly TestResultReportingServiceSettings serviceSpecificSettings;
        Timer timer;

        public TestResultReportingService(ITestOperationResultStorage storage)
        {
            this.storage = Preconditions.CheckNotNull(storage, nameof(storage));
            this.delayBeforeWork = Settings.Current.TestStartDelay + Settings.Current.TestDuration + Settings.Current.DurationBeforeVerification;
            this.serviceSpecificSettings = Settings.Current.TestResultReportingServiceSettings.Expect(() => new ArgumentException("TestResultReportingServiceSettings must be supplied."));
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
            var tesReportGeneratorFactory = new TestReportGeneratorFactory(this.storage, Settings.Current.NetworkControllerType);
            List<ITestReportMetadata> reportMetadataList = await Settings.Current.GetReportMetadataListAsync(this.logger);
            ITestResultReport[] testResultReports = await TestReportUtil.GenerateTestResultReportsAsync(Settings.Current.TrackingId, reportMetadataList, tesReportGeneratorFactory, this.logger);

            if (testResultReports.Length == 0)
            {
                this.logger.LogInformation("No test result report is generated.");
                return;
            }

            string blobContainerUri = string.Empty;

            if (this.serviceSpecificSettings.LogUploadEnabled)
            {
                try
                {
                    Uri blobContainerWriteUriForLog = await TestReportUtil.GetOrCreateBlobContainerSasUriForLogAsync(this.serviceSpecificSettings.StorageAccountConnectionString);
                    blobContainerUri = $"{blobContainerWriteUriForLog.Scheme}{Uri.SchemeDelimiter}{blobContainerWriteUriForLog.Authority}{blobContainerWriteUriForLog.AbsolutePath}";
                    await TestReportUtil.UploadLogsAsync(Settings.Current.IoTHubConnectionString, blobContainerWriteUriForLog, this.logger);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Exception happened when uploading logs");
                }
            }

            var testSummary = new TestSummary(Settings.Current.TestInfo, testResultReports, blobContainerUri);
            string reportsContent = JsonConvert.SerializeObject(testSummary, Formatting.Indented);
            this.logger.LogInformation($"Test summary{Environment.NewLine}{reportsContent}");

            await AzureLogAnalytics.Instance.PostAsync(
                this.serviceSpecificSettings.LogAnalyticsWorkspaceId,
                this.serviceSpecificSettings.LogAnalyticsSharedKey,
                reportsContent,
                this.serviceSpecificSettings.LogAnalyticsLogType);

            this.logger.LogInformation("Successfully send reports to LogAnalytics");
        }
    }
}
