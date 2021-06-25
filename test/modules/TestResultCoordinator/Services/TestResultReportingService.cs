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
        readonly TimeSpan sendReportFrequency;
        readonly Option<TimeSpan> logUploadDuration;
        Timer timer;

        public TestResultReportingService(ITestOperationResultStorage storage)
        {
            this.storage = Preconditions.CheckNotNull(storage, nameof(storage));
            switch (Settings.Current.TestMode)
            {
                case TestMode.Connectivity:
                    {
                        ConnectivitySpecificSettings connectivitySettings =
                            Settings.Current.ConnectivitySpecificSettings.Expect(() => new ArgumentException("ConnectivitySpecificSettings must be supplied."));
                        this.delayBeforeWork = Settings.Current.TestStartDelay +
                            connectivitySettings.TestDuration +
                            connectivitySettings.TestVerificationDelay;
                        // Set sendReportFrequency to -1ms to indicate that the sending report Timer won't repeat
                        this.sendReportFrequency = TimeSpan.FromMilliseconds(-1);
                        this.logUploadDuration = Option.None<TimeSpan>();
                        break;
                    }

                case TestMode.LongHaul:
                    {
                        // In long haul mode, wait 1 report frequency before starting
                        this.sendReportFrequency = Settings.Current.LongHaulSpecificSettings
                            .Expect(() => new ArgumentException("LongHaulSpecificSettings must be supplied."))
                            .SendReportFrequency;
                        this.delayBeforeWork = this.sendReportFrequency;
                        // Add 10 minute buffer to duration to ensure we capture all logs
                        this.logUploadDuration = Option.Some(this.sendReportFrequency + TimeSpan.FromMinutes(10));
                        break;
                    }
            }

            this.serviceSpecificSettings = Settings.Current.TestResultReportingServiceSettings.Expect(() => new ArgumentException("TestResultReportingServiceSettings must be supplied."));
        }

        public Task StartAsync(CancellationToken ct)
        {
            this.logger.LogInformation("Test Result Reporting Service running.");

            this.timer = new Timer(
                this.DoWorkAsync,
                null,
                this.delayBeforeWork,
                this.sendReportFrequency);

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
            var testReportGeneratorFactory = new TestReportGeneratorFactory(this.storage, Settings.Current.NetworkControllerType, Settings.Current.LongHaulSpecificSettings);
            List<ITestReportMetadata> reportMetadataList = await Settings.Current.GetReportMetadataListAsync(this.logger);
            ITestResultReport[] testResultReports = await TestReportUtil.GenerateTestResultReportsAsync(Settings.Current.TrackingId, reportMetadataList, testReportGeneratorFactory, this.logger);

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

                    await TestReportUtil.UploadLogsAsync(Settings.Current.IoTHubConnectionString, blobContainerWriteUriForLog, this.logUploadDuration, this.logger);
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
