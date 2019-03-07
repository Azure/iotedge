// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class ConsoleEndpointTest : RoutingUnitTestBase
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new ConsoleEndpoint(null));
        }

        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            using (var sw = new StringWriter())
            {
                Console.SetOut(sw);
                var console = new ConsoleEndpoint("id1");
                IMessage[] messages = new[] { Message1 };
                IProcessor processor = console.CreateProcessor();
                await processor.ProcessAsync(new IMessage[0], CancellationToken.None);
                await processor.ProcessAsync(messages, CancellationToken.None);
                await processor.CloseAsync(CancellationToken.None);

                string expectedWindows = $"(0) ConsoleEndpoint(id1): {Message1}\r\n";
                string expectedLinux = $"(0) ConsoleEndpoint(id1): {Message1}\n";
                Assert.True(
                    expectedWindows.Equals(sw.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    expectedLinux.Equals(sw.ToString(), StringComparison.OrdinalIgnoreCase));
                Assert.Equal(console, processor.Endpoint);
                Assert.True(processor.ErrorDetectionStrategy.IsTransient(new Exception()));

                var console2 = new ConsoleEndpoint("id1", "name1", "hub1", ConsoleColor.Red);
                Assert.Equal("id1", console2.Id);
                Assert.Equal("name1", console2.Name);
                Assert.Equal("hub1", console2.IotHubName);
            }
        }
    }
}
