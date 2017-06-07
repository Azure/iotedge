// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core;
    using Xunit;

    [Unit]
    public class NullRoutingPerfCounterTest
    {
        [Fact]
        public void LogEventProcessingLatency()
        {
            Assert.True(NullRoutingPerfCounter.Instance.LogEventProcessingLatency(null, null, null, null, 0, out string resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogE2EEventProcessingLatency()
        {
            Assert.True(NullRoutingPerfCounter.Instance.LogE2EEventProcessingLatency(null, null, null, null, 0, out string resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogEventsProcessed()
        {
            Assert.True(NullRoutingPerfCounter.Instance.LogEventsProcessed(null, null, null, null, 0, out string resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogInternalEventHubReadLatency()
        {
            Assert.True(NullRoutingPerfCounter.Instance.LogInternalEventHubReadLatency(null, 0, false, 0, out string resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogInternalEventHubEventsRead()
        {
            Assert.True(NullRoutingPerfCounter.Instance.LogInternalEventHubEventsRead(null, 0, false, 0, out string resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogInternalProcessingLatency()
        {
            Assert.True(NullRoutingPerfCounter.Instance.LogInternalProcessingLatency(null, 0, false, 0, out string resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogExternalWriteLatency()
        {
            Assert.True(NullRoutingPerfCounter.Instance.LogExternalWriteLatency(null, null, null, false, 0, out string resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogMessageEndpointsMatched()
        {
            Assert.True(NullRoutingPerfCounter.Instance.LogMessageEndpointsMatched(null, null, 0, out string resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogUnmatchedMessages()
        {
            Assert.True(NullRoutingPerfCounter.Instance.LogUnmatchedMessages(null, null, 0, out string resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogCheckpointStoreLatency()
        {
            Assert.True(NullRoutingPerfCounter.Instance.LogCheckpointStoreLatency(null, null, null, null, null, 0, out string resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogOperationResult()
        {
            Assert.True(NullRoutingPerfCounter.Instance.LogOperationResult(null, null, null, 0, out string resultString));
            Assert.Equal(string.Empty, resultString);
        }
    }
}
