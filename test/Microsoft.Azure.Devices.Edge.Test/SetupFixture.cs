// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using NUnit.Framework;
    using Serilog;
    using Serilog.Events;

    [SetUpFixture]
    public class SetupFixture
    {
        [OneTimeSetUp]
        public void SetUpLogging()
        {
            LogEventLevel consoleLevel = Context.Current.Verbose
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.NUnit(consoleLevel);
            Context.Current.LogFile.ForEach(f => loggerConfig.WriteTo.File(f));
            Log.Logger = loggerConfig.CreateLogger();
        }

        [OneTimeTearDown]
        public void CloseLogger() => Log.CloseAndFlush();
    }
}
