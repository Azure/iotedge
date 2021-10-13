// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using OpenTelemetry;
    using OpenTelemetry.Trace;

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

        private Activity activity;

        protected async Task SendEventAsync(long messageId, string outputName)
        {
            var random = new Random();
            var bufferPool = new BufferPool();

            using (Buffer data = bufferPool.AllocBuffer(Settings.Current.MessageSizeInBytes))
            {
                // generate some bytes
                random.NextBytes(data.Data);
                if (activity != null)
                {
                    Logger.LogInformation("Disposing Previous Activity");
                    activity.Dispose();
                }
                activity = Settings.activitySource.StartActivity("SendEventAsync", ActivityKind.Producer);
                Logger.LogInformation($"Activity TraceId is {activity?.TraceId}");
                Logger.LogInformation($"Activity SpanId is {activity?.SpanId}");
                activity?.AddEvent(new ActivityEvent($"Sending Message with BatchID : {this.BatchId.ToString()}"));
                // build message
                var messageBody = new { data = data.Data };
                var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(messageBody)));
                message.Properties.Add(TestConstants.Message.SequenceNumberPropertyName, messageId.ToString());
                message.Properties.Add(TestConstants.Message.BatchIdPropertyName, this.BatchId.ToString());
                message.Properties.Add(TestConstants.Message.TrackingIdPropertyName, this.TrackingId);
                message.Properties.Add(TestConstants.Message.TraceIdPropertyName, activity?.TraceId.ToString());
                message.Properties.Add(TestConstants.Message.SpanIdPropertyName, activity?.SpanId.ToString());
                // sending the result via edgeHub
                await this.Client.SendEventAsync(outputName, message);
                this.Logger.LogInformation($"Sent message successfully: sequenceNumber={messageId}");
            }
        }

        protected async Task ReportResult(long messageIdCounter)
        {
            if (activity != null)
            {
                Logger.LogInformation("Previous Activity Not NUll");
                activity?.AddEvent(new ActivityEvent($"Sending Results with BatchID : {this.BatchId.ToString()}"));
            }
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
