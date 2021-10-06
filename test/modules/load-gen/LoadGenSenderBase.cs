// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public abstract class LoadGenSenderBase
    {
        public LoadGenSenderBase(
            ILogger logger,
            ModuleClient moduleClient,
            Guid batchId,
            string trackingId)
        {
            this.Logger = Preconditions.CheckNotNull(logger, nameof(logger));
            this.Client = Preconditions.CheckNotNull(moduleClient, nameof(moduleClient));
            this.BatchId = Preconditions.CheckNotNull(batchId, nameof(batchId));
            this.TrackingId = trackingId ?? string.Empty;
        }

        public ILogger Logger { get; }

        public ModuleClient Client { get; }

        public Guid BatchId { get; }

        public string TrackingId { get; }

        public abstract Task RunAsync(CancellationTokenSource cts, DateTime testStartAt);

        protected async Task SendEventAsync(long messageId, string outputName)
        {
            var random = new Random();
            var bufferPool = new BufferPool();

            using (Buffer data = bufferPool.AllocBuffer((ulong)Settings.Current.MessageSizeInBytes))
            {
                // generate some bytes
                random.NextBytes(data.Data);

                // build message
                var messageBody = new { data = data.Data };
                var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                message.Properties.Add(TestConstants.Message.SequenceNumberPropertyName, messageId.ToString());
                message.Properties.Add(TestConstants.Message.BatchIdPropertyName, this.BatchId.ToString());
                message.Properties.Add(TestConstants.Message.TrackingIdPropertyName, this.TrackingId);

                // sending the result via edgeHub
                await this.Client.SendEventAsync(outputName, message);
                this.Logger.LogInformation($"Sent message successfully: sequenceNumber={messageId}");
            }
        }

        protected async Task ReportResult(long messageIdCounter)
        {
            await Settings.Current.TestResultCoordinatorUrl.ForEachAsync(
                async trcUrl =>
                {
                    var testResultCoordinatorUrl = new Uri(trcUrl, UriKind.Absolute);
                    var testResultReportingClient = new TestResultReportingClient { BaseUrl = testResultCoordinatorUrl.AbsoluteUri };
                    var testResult = new MessageTestResult(Settings.Current.ModuleId + ".send", DateTime.UtcNow)
                    {
                        TrackingId = this.TrackingId,
                        BatchId = this.BatchId.ToString(),
                        SequenceNumber = messageIdCounter.ToString()
                    };
                    await ModuleUtil.ReportTestResultAsync(testResultReportingClient, this.Logger, testResult);
                });
        }
    }
}
