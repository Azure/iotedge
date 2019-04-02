// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Configuration;
    using Serilog.Core;
    using Serilog.Events;
    using Xunit;
    using ILogger = Microsoft.Extensions.Logging.ILogger;
    using Logger = Microsoft.Azure.Devices.Edge.Util.Logger;

    [Unit]
    public class LoggerTest
    {
        [Fact]
        public void SeverityTest()
        {
            var testSink = new TestSink();

            LoggerConfiguration TestSinkMap(LoggerSinkConfiguration loggerSinkConfiguration)
                => loggerSinkConfiguration.Sink(testSink);

            ILogger logger = Logger.GetLoggerFactory(LogEventLevel.Debug, TestSinkMap).CreateLogger("test");
            Assert.NotNull(logger);

            logger.LogInformation("Test message");
            List<LogEvent> emittedEvents = testSink.GetEmittedEvents();
            Assert.Equal("6", emittedEvents[0].Properties["Severity"].ToString());
            testSink.Reset();

            logger.LogDebug("Test message");
            emittedEvents = testSink.GetEmittedEvents();
            Assert.Equal("7", emittedEvents[0].Properties["Severity"].ToString());
            testSink.Reset();

            logger.LogWarning("Test message");
            emittedEvents = testSink.GetEmittedEvents();
            Assert.Equal("4", emittedEvents[0].Properties["Severity"].ToString());
            testSink.Reset();

            logger.LogError("Test message");
            emittedEvents = testSink.GetEmittedEvents();
            Assert.Equal("3", emittedEvents[0].Properties["Severity"].ToString());
        }

        class TestSink : ILogEventSink
        {
            List<LogEvent> emittedEvents = new List<LogEvent>();

            public void Emit(LogEvent logEvent) => this.emittedEvents.Add(logEvent);

            public List<LogEvent> GetEmittedEvents() => this.emittedEvents;

            public void Reset() => this.emittedEvents = new List<LogEvent>();
        }
    }
}
