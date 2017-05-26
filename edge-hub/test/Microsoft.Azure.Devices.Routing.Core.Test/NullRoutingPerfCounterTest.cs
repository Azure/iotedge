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
            var nullRoutingPerfCounter = new NullRoutingPerfCounter();
            string resultString;

            Assert.True(nullRoutingPerfCounter.LogEventProcessingLatency(null, null, null, null, 0, out resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogE2EEventProcessingLatency()
        {
            var nullRoutingPerfCounter = new NullRoutingPerfCounter();
            string resultString;

            Assert.True(nullRoutingPerfCounter.LogE2EEventProcessingLatency(null, null, null, null, 0, out resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogEventsProcessed()
        {
            var nullRoutingPerfCounter = new NullRoutingPerfCounter();
            string resultString;

            Assert.True(nullRoutingPerfCounter.LogEventsProcessed(null, null, null, null, 0, out resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogInternalEventHubReadLatency()
        {
            var nullRoutingPerfCounter = new NullRoutingPerfCounter();
            string resultString;

            Assert.True(nullRoutingPerfCounter.LogInternalEventHubReadLatency(null, 0, false, 0, out resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogInternalEventHubEventsRead()
        {
            var nullRoutingPerfCounter = new NullRoutingPerfCounter();
            string resultString;

            Assert.True(nullRoutingPerfCounter.LogInternalEventHubEventsRead(null, 0, false, 0, out resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogInternalProcessingLatency()
        {
            var nullRoutingPerfCounter = new NullRoutingPerfCounter();
            string resultString;

            Assert.True(nullRoutingPerfCounter.LogInternalProcessingLatency(null, 0, false, 0, out resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogExternalWriteLatency()
        {
            var nullRoutingPerfCounter = new NullRoutingPerfCounter();
            string resultString;

            Assert.True(nullRoutingPerfCounter.LogExternalWriteLatency(null, null, null, false, 0, out resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogMessageEndpointsMatched()
        {
            var nullRoutingPerfCounter = new NullRoutingPerfCounter();
            string resultString;

            Assert.True(nullRoutingPerfCounter.LogMessageEndpointsMatched(null, null, 0, out resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogUnmatchedMessages()
        {
            var nullRoutingPerfCounter = new NullRoutingPerfCounter();
            string resultString;

            Assert.True(nullRoutingPerfCounter.LogUnmatchedMessages(null, null, 0, out resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogCheckpointStoreLatency()
        {
            var nullRoutingPerfCounter = new NullRoutingPerfCounter();
            string resultString;

            Assert.True(nullRoutingPerfCounter.LogCheckpointStoreLatency(null, null, null, null, null, 0, out resultString));
            Assert.Equal(string.Empty, resultString);
        }

        [Fact]
        public void LogOperationResult()
        {
            var nullRoutingPerfCounter = new NullRoutingPerfCounter();
            string resultString;

            Assert.True(nullRoutingPerfCounter.LogOperationResult(null, null, null, 0, out resultString));
            Assert.Equal(string.Empty, resultString);
        }
    }
}
