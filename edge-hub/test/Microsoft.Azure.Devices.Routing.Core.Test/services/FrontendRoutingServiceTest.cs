// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Services;
    using Microsoft.Azure.Devices.Routing.Core.Test.Sinks;

    using Moq;

    using Xunit;

    [Unit]
    public class FrontendRoutingServiceTest
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 1 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage Message4 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key", "value" }, { "key2", "value2" } });

        [Fact]
        public async Task SmokeTest()
        {
            var sink1 = new TestSink<IMessage>();
            var sink2 = new TestSink<IMessage>();
            var sinkFactory = new Mock<ISinkFactory<IMessage>>();
            sinkFactory.Setup(s => s.CreateAsync("hub1")).ReturnsAsync(sink1);
            sinkFactory.Setup(s => s.CreateAsync("hub2")).ReturnsAsync(sink2);
            sinkFactory.Setup(s => s.CreateAsync("nohub")).Throws(new InvalidOperationException());

            using (var client = new FrontendRoutingService(sinkFactory.Object, NullNotifierFactory.Instance))
            {
                await client.StartAsync();
                await client.RouteAsync("hub1", Message1);
                await client.RouteAsync("hub2", Message2);
                await client.RouteAsync("hub2", Message4);
                await client.RouteAsync("hub1", Message3);

                Assert.Equal(new List<IMessage> { Message1, Message3 }, sink1.Processed);
                Assert.Equal(new List<IMessage> { Message2, Message4 }, sink2.Processed);

                await Assert.ThrowsAsync<InvalidOperationException>(() => client.RouteAsync("nohub", Message1));

                await client.CloseAsync(CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(() => client.RouteAsync("hub1", Message1));
            }
        }

        [Fact]
        public async Task TestClosed()
        {
            var sinkFactory = new TestSinkFactory<IMessage>();
            var client = new FrontendRoutingService(sinkFactory, NullNotifierFactory.Instance);

            await client.RouteAsync("hub1", Message1);
            await client.RouteAsync("hub2", Message2);
            await client.RouteAsync("hub2", Message4);
            await client.RouteAsync("hub1", Message3);

            var sink1 = (TestSink<IMessage>)await sinkFactory.CreateAsync("hub1");
            var sink2 = (TestSink<IMessage>)await sinkFactory.CreateAsync("hub2");
            Assert.False(sink1.IsClosed);
            Assert.False(sink2.IsClosed);

            await client.CloseAsync(CancellationToken.None);

            Assert.True(sink1.IsClosed);
            Assert.True(sink2.IsClosed);
        }

        [Fact]
        public async Task TestFailedSink()
        {
            var sinkFactory = new FailedSinkFactory<IMessage>(new Exception("failure"));
            var client = new FrontendRoutingService(sinkFactory, NullNotifierFactory.Instance);

            Exception ex1 = await Assert.ThrowsAsync<Exception>(() => client.RouteAsync("hub1", Message1));
            Exception ex2 = await Assert.ThrowsAsync<Exception>(() => client.RouteAsync("hub2", Message2));
            Assert.Equal("failure", ex1.Message);
            Assert.Equal("failure", ex2.Message);

            // check doesn't throw on close
            await client.CloseAsync(CancellationToken.None);
        }

        [Fact]
        public async Task TestHubDeleted()
        {
            var notifier = new TestNotifier();
            var notifierFactory = new TestNotifierFactory(notifier);

            var sink1 = new TestSink<IMessage>();
            var sink2 = new TestSink<IMessage>();
            var sinkFactory = new Mock<ISinkFactory<IMessage>>();
            sinkFactory.Setup(s => s.CreateAsync("hub1")).ReturnsAsync(sink1);
            sinkFactory.Setup(s => s.CreateAsync("hub2")).ReturnsAsync(sink2);
            var client = new FrontendRoutingService(sinkFactory.Object, notifierFactory);

            await client.RouteAsync("hub1", Message1);
            await client.RouteAsync("hub2", Message2);
            await client.RouteAsync("hub2", Message4);
            await client.RouteAsync("hub1", Message3);

            Assert.False(sink1.IsClosed);
            Assert.False(sink2.IsClosed);

            await notifier.Delete("nohub");
            await notifier.Delete("hub2");
            sinkFactory.Setup(s => s.CreateAsync("hub2")).Throws(new InvalidOperationException());

            await client.RouteAsync("hub1", Message1);
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.RouteAsync("hub2", Message2));
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.RouteAsync("hub2", Message4));
            await client.RouteAsync("hub1", Message3);

            Assert.False(sink1.IsClosed);
            Assert.True(sink2.IsClosed);

            await client.CloseAsync(CancellationToken.None);

            Assert.True(sink1.IsClosed);
            Assert.True(sink2.IsClosed);

            Assert.Equal(new List<IMessage> { Message1, Message3, Message1, Message3 }, sink1.Processed);
            Assert.Equal(new List<IMessage> { Message2, Message4 }, sink2.Processed);
        }
    }
}
