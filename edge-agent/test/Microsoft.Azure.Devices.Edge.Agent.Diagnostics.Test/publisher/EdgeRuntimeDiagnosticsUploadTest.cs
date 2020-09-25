// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Akka.Event;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class EdgeRuntimeDiagnosticsUploadTest
    {
        Message[] lastUploadResult;
        IEdgeAgentConnection mockConnection;

        public EdgeRuntimeDiagnosticsUploadTest()
        {
            var connectionMock = new Mock<IEdgeAgentConnection>();
            connectionMock.Setup(c => c.SendEventBatchAsync(It.IsAny<IEnumerable<Message>>())).Callback((Action<IEnumerable<Message>>)(data => this.lastUploadResult = data.ToArray())).Returns(Task.CompletedTask);

            this.mockConnection = connectionMock.Object;
        }

        [Fact]
        public async Task BasicFunctionality()
        {
            var uploader = new EdgeRuntimeDiagnosticsUpload(this.mockConnection);

            Metric expectedMetric = new Metric(DateTime.UtcNow, "test_metric", 10, new Dictionary<string, string> { { "tag1", "asdf" }, { "tag2", "fdsa" } });

            await uploader.PublishAsync(new Metric[] { expectedMetric }, CancellationToken.None);

            Message uploadResult = this.lastUploadResult.Single();
            Assert.Equal("application/x-azureiot-edgeruntimediagnostics", uploadResult.ContentType);

            Metric uploadedMetric = MetricsSerializer.BytesToMetrics(uploadResult.GetBytes()).Single();
            Assert.Equal(expectedMetric, uploadedMetric);
        }

        [Fact]
        public async Task SendsMultipleMetrics()
        {
            var uploader = new EdgeRuntimeDiagnosticsUpload(this.mockConnection);
            Metric[] expectedMetrics = this.GetFakeMetrics(20);

            await uploader.PublishAsync(expectedMetrics, CancellationToken.None);

            Metric[] uploadedMetrics = this.ParseLastUploadResult();
            TestUtilities.OrderlessCompare(expectedMetrics, uploadedMetrics);
        }

        [Fact]
        public async Task BatchesMetrics()
        {
            var uploader = new EdgeRuntimeDiagnosticsUpload(this.mockConnection);

            // Doesn't batch small numbers of metrics
            Metric[] singleBatch = this.GetFakeMetrics(20);
            await uploader.PublishAsync(singleBatch, CancellationToken.None);
            Assert.Single(this.lastUploadResult);
            TestUtilities.OrderlessCompare(singleBatch, this.ParseLastUploadResult());

            // correctly batches even number of metrics
            Metric[] evenBatch = this.GetFakeMetrics(20000);
            await uploader.PublishAsync(evenBatch, CancellationToken.None);
            Assert.True(this.lastUploadResult.Length > 1, $"Expected more than 1 uploaded message. Got {this.lastUploadResult.Length}");
            TestUtilities.OrderlessCompare(evenBatch, this.ParseLastUploadResult());

            // correctly batches odd number of metrics
            Metric[] oddBatch = this.GetFakeMetrics(20001);
            await uploader.PublishAsync(oddBatch, CancellationToken.None);
            Assert.True(this.lastUploadResult.Length > 1, $"Expected more than 1 uploaded message. Got {this.lastUploadResult.Length}");
            TestUtilities.OrderlessCompare(oddBatch, this.ParseLastUploadResult());
        }

        Metric[] GetFakeMetrics(int n)
        {
            return Enumerable.Range(1, n).Select(i => new Metric(DateTime.UtcNow, $"test_metric_{i}", i, new Dictionary<string, string> { { "tag1", $"asdf{i}" }, { "tag2", $"fdsa{i}" } })).ToArray();
        }

        Metric[] ParseLastUploadResult()
        {
            return this.lastUploadResult.SelectMany(message => MetricsSerializer.BytesToMetrics(message.GetBytes())).ToArray();
        }
    }
}
