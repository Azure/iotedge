// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class MessageTest : RoutingUnitTestBase
    {
        static readonly Message Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly Message Message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly Message Message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 1 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly Message Message4 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key", "value" }, { "key2", "value2" } });
        static readonly Message Message5 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key", "value" } });
        static readonly Message Message6 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, new Dictionary<string, string> { { "sys1", "value1" } });
        static readonly Message Message7 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, new Dictionary<string, string> { { "sys1", "value1" } });
        static readonly Message Message8 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, new Dictionary<string, string> { { "sys1", "value2" } });

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new Message(TelemetryMessageSource.Instance, new byte[0], null));
        }

        [Fact]
        [Unit]
        public void TestEquals()
        {
            Assert.Equal(Message1, Message1);
            Assert.Equal(Message1, Message2);
            Assert.NotEqual(Message1, Message3);
            Assert.NotEqual(Message1, Message4);
            Assert.NotEqual(Message1, Message5);
            Assert.NotEqual(Message1, Message6);
            Assert.Equal(Message6, Message7);
            Assert.NotEqual(Message6, Message8);

            Assert.False(Message1.Equals(null));

            Assert.True(Message1.Equals(Message1));
            Assert.False(Message1.Equals(Message3));

            Assert.False(Message1.Equals(null));
            Assert.False(Message1.Equals((object)null));
            Assert.True(Message1.Equals((object)Message1));
            Assert.False(Message1.Equals((object)Message3));
            Assert.False(Message1.Equals(new object()));
        }

        [Fact]
        [Unit]
        public void TestCaseSensitivity()
        {
            var message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string>(StringComparer.InvariantCulture) { { "KEY1", "value1" }, { "key2", "value2" } });
            Assert.Equal("value1", message1.Properties["key1"]);
            Assert.Equal("value2", message1.Properties["key2"]);
        }

        [Fact]
        [Unit]
        public void TestSet()
        {
            var messages = new HashSet<Message>
            {
                Message1,
                Message2,
                Message3,
                Message4,
                Message6,
                Message7
            };

            Assert.Equal(4, messages.Count);
            Assert.Contains(Message1, messages);
            Assert.Contains(Message3, messages);
            Assert.Contains(Message4, messages);
            Assert.Contains(Message6, messages);
            Assert.DoesNotContain(Message5, messages);
        }
    }
}
