// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.IO;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Xunit;

    [Unit]
    public class LoggerTest
    {
        [Fact]
        public void SeverityTest()
        {
            Logger.SetLogLevel("debug");
            ILogger logger = Logger.Factory.CreateLogger("Test");

            using (StringWriter sw = new StringWriter())
            {
                Console.SetOut(sw);
                logger.LogInformation("Test message");
                string output = sw.ToString();
                Assert.True(output.StartsWith("<6>"));
            }

            using (StringWriter sw = new StringWriter())
            {
                Console.SetOut(sw);
                logger.LogDebug("Test message");
                string output = sw.ToString();
                Assert.True(output.StartsWith("<7>"));
            }

            using (StringWriter sw = new StringWriter())
            {
                Console.SetOut(sw);
                logger.LogWarning("Test message");
                string output = sw.ToString();
                Assert.True(output.StartsWith("<4>"));
            }

            using (StringWriter sw = new StringWriter())
            {
                Console.SetOut(sw);
                logger.LogError("Test message");
                string output = sw.ToString();
                Assert.True(output.StartsWith("<3>"));
            }
        }
    }
}
