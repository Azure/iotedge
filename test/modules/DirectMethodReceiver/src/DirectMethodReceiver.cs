// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodReceiver
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    class DirectMethodReceiver : IDisposable
    {
        Guid batchId;
        IConfiguration configuration;
        long directMethodCount = 1;
        ILogger logger;
        ModuleClient moduleClient;
        TestResultReportingClient testResultReportingClient;

        public DirectMethodReceiver(
            ILogger logger,
            IConfiguration configuration,
            ModuleClient moduleClient,
            TestResultReportingClient testResultReportingClient)
        {
            Preconditions.CheckNotNull(logger, nameof(logger));
            Preconditions.CheckNotNull(configuration, nameof(configuration));
            Preconditions.CheckNotNull(moduleClient, nameof(moduleClient));

            this.logger = logger;
            this.configuration = configuration;
            this.moduleClient = moduleClient;
            this.testResultReportingClient = testResultReportingClient;
            this.batchId = Guid.NewGuid();
        }

        public static async Task<DirectMethodReceiver> CreateAsync(
            ILogger logger,
            IConfiguration configuration)
        {
            Preconditions.CheckNotNull(logger, nameof(logger));
            Preconditions.CheckNotNull(configuration, nameof(configuration));

            ModuleClient moduleClient = await ModuleUtil.CreateModuleClientAsync(
                configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only),
                ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                ModuleUtil.DefaultTransientRetryStrategy,
                logger);

            TestResultReportingClient testResultReportingClient = null;
            Option<Uri> testReportCoordinatorUrl = Option.Maybe(configuration.GetValue<Uri>("testResultCoordinatorUrl"));
            if (testReportCoordinatorUrl.HasValue)
            {
                testResultReportingClient = new TestResultReportingClient
                {
                    BaseUrl = testReportCoordinatorUrl.Expect(
                        () => new ArgumentException("testReportCoordinatorUrl is empty"))
                        .AbsoluteUri
                };
            }

            return new DirectMethodReceiver(
                logger,
                configuration,
                moduleClient,
                testResultReportingClient);
        }

        public void Dispose() => this.moduleClient?.Dispose();

        async Task<MethodResponse> HelloWorldMethodAsync(MethodRequest methodRequest, object userContext)
        {
            this.logger.LogInformation("Received direct method call.");
            // Send the report to Test Result Coordinator
            await this.ReportTestResult();
            return new MethodResponse((int)HttpStatusCode.OK);
        }

        public async Task ReportTestResult()
        {
            DirectMethodTestResult testResult = null;
            if (this.testResultReportingClient != null)
            {
                testResult = new DirectMethodTestResult(this.configuration.GetValue<string>("IOTEDGE_MODULEID") + ".receive", DateTime.UtcNow)
                {
                    TrackingId = Option.Maybe(this.configuration.GetValue<string>("trackingId"))
                        .Expect(() => new ArgumentException("TrackingId is empty")),
                    BatchId = this.batchId.ToString(),
                    SequenceNumber = this.directMethodCount.ToString(),
                    Result = HttpStatusCode.OK.ToString()
                };
            }

            await ModuleUtil.ReportTestResultAsync(this.testResultReportingClient, this.logger, testResult);
            this.directMethodCount++;
        }

        public async Task StartAsync()
        {
            await this.moduleClient.OpenAsync();
            await this.moduleClient.SetMethodHandlerAsync("HelloWorldMethod", this.HelloWorldMethodAsync, null);
        }
    }
}