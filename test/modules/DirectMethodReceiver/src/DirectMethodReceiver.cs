// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodReceiver
{
    using System;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    class DirectMethodReceiver : IDisposable
    {
        Guid batchId;
        IConfiguration configuration;
        ILogger logger;
        ModuleClient moduleClient;
        Option<TestResultReportingClient> testResultReportingClient;
        Option<string> trackingId;

        public DirectMethodReceiver(
            ILogger logger,
            IConfiguration configuration)
        {
            Preconditions.CheckNotNull(logger, nameof(logger));
            Preconditions.CheckNotNull(configuration, nameof(configuration));

            Option<Uri> testReportCoordinatorUrl = Option.Maybe(configuration.GetValue<Uri>("ReportingEndpointUrl"));
            testReportCoordinatorUrl.ForEach(
                (Uri uri) => this.testResultReportingClient = Option.Some<TestResultReportingClient>(new TestResultReportingClient { BaseUrl = uri.AbsoluteUri }),
                () => this.testResultReportingClient = Option.None<TestResultReportingClient>());

            this.logger = logger;
            this.configuration = configuration;
            this.batchId = Guid.NewGuid();
            this.trackingId = testReportCoordinatorUrl.HasValue ? Option.Some<string>(this.configuration.GetValue<string>("trackingId")) : Option.None<string>();
        }

        public void Dispose() => this.moduleClient?.Dispose();

        async Task<MethodResponse> HelloWorldMethodAsync(MethodRequest methodRequest, object userContext)
        {
            try
            {
                this.logger.LogInformation($"Received direct method call: {methodRequest.DataAsJson}");
                JToken payload = JToken.Parse(methodRequest.DataAsJson);
                // Send the report to Test Result Coordinator
                await this.ReportTestResult(payload["DirectMethodCount"].ToString());
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Exception thrown from {nameof(this.HelloWorldMethodAsync)} method");
            }

            return new MethodResponse((int)HttpStatusCode.OK);
        }

        public async Task ReportTestResult(string directMethodCount)
        {
            await this.testResultReportingClient.ForEachAsync(
                async (TestResultReportingClient testResultReportingClient) =>
                    {
                        DirectMethodTestResult testResult = new DirectMethodTestResult(
                            this.configuration.GetValue<string>("IOTEDGE_MODULEID") + ".receive",
                            DateTime.UtcNow,
                            this.trackingId.GetOrElse(string.Empty),
                            this.batchId,
                            ulong.Parse(directMethodCount),
                            HttpStatusCode.OK);

                        await ModuleUtil.ReportTestResultAsync(testResultReportingClient, this.logger, testResult);
                    });
        }

        public async Task InitAsync()
        {
            this.moduleClient = await ModuleUtil.CreateModuleClientAsync(
                this.configuration.GetValue("ClientTransportType", TransportType.Amqp_Tcp_Only),
                new ClientOptions(),
                ModuleUtil.DefaultTimeoutErrorDetectionStrategy,
                ModuleUtil.DefaultTransientRetryStrategy,
                this.logger);

            await this.moduleClient.OpenAsync();
            await this.moduleClient.SetMethodHandlerAsync("HelloWorldMethod", this.HelloWorldMethodAsync, null);
        }
    }
}
