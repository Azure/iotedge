// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.IO;
    using NUnit.Framework;
    using Serilog;
    using Serilog.Configuration;
    using Serilog.Core;
    using Serilog.Events;
    using Serilog.Formatting;

    public class NUnitSink : ILogEventSink
    {
        /*
         * It would be nice to use the TextWriter sink that comes with Serilog,
         * but it doesn't play well with NUnit--each token in the message
         * template ends up on a new line. This custom sink renders the
         * complete message as a string before sending it to NUnit's TextWriter
         * (NUnit.Framework.TestContext.Progress).
         */

        readonly ITextFormatter formatter;

        public NUnitSink(ITextFormatter formatter)
        {
            this.formatter = formatter;
        }

        public void Emit(LogEvent logEvent)
        {
            var writer = new StringWriter();
            this.formatter.Format(logEvent, writer);
            TestContext.Progress.Write(writer);
            TestContext.Progress.Flush();
        }
    }

    public static class NUnitExtensions
    {
        public static LoggerConfiguration NUnit(
            this LoggerSinkConfiguration loggerConfiguration,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            IFormatProvider formatProvider = null,
            LoggingLevelSwitch levelSwitch = null)
        {
            string outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";
            var formatter = new Serilog.Formatting.Display.MessageTemplateTextFormatter(outputTemplate, formatProvider);
            return loggerConfiguration.Sink(new NUnitSink(formatter), restrictedToMinimumLevel, levelSwitch);
        }
    }
}
