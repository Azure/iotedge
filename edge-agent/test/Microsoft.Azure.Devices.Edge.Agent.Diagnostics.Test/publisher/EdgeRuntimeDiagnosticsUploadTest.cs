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
    }
}
