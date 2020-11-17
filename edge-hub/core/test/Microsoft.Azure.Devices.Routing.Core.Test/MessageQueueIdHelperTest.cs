// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using System;
    using Xunit;

    public class MessageQueueIdHelperTest
    {
        [Fact]
        [Unit]
        public void TestGetMessageQueueIdWithDefaultPriority()
        {
            // Message queue with default priority should remain as endpoint id
            var endpointId = Guid.NewGuid().ToString();
            var messageQueueId = MessageQueueIdHelper.GetMessageQueueId(endpointId, Core.RouteFactory.DefaultPriority);
            Assert.Equal(endpointId, messageQueueId);
        }

        [Fact]
        [Unit]
        public void TestGetMessageQueueIdWithNonDefaultPriority()
        {
            // Message queue with non-default priority should combain endpoint id and priority with MessageQueueIdHelper.MessageQueueIdDelimiter
            var endpointId = Guid.NewGuid().ToString();
            uint priority = 1234;
            var expectMessageQueueId = $"{endpointId}{MessageQueueIdHelper.MessageQueueIdDelimiter}{priority}";

            var messageQueueId = MessageQueueIdHelper.GetMessageQueueId(endpointId, priority);

            Assert.Equal(expectMessageQueueId, messageQueueId);
        }

        [Fact]
        [Unit]
        public void TestParseMessageQueueIdWithoutPriority()
        {
            // Message queue id without priority should return default priority
            var expectedEndpointId = Guid.NewGuid().ToString();
            var messageQueueId = expectedEndpointId;
            var expectedPriority = Core.RouteFactory.DefaultPriority;

            var (endpointId, priority) = MessageQueueIdHelper.ParseMessageQueueId(messageQueueId);

            Assert.Equal(expectedEndpointId, endpointId);
            Assert.Equal(expectedPriority, priority);
        }

        [Fact]
        [Unit]
        public void TestParseMessageQueueIdWithPriority()
        {
            // Message queue id with priority should return its priority
            var expectedEndpointId = Guid.NewGuid().ToString();
            uint expectedPriority = 1234;
            var messageQueueId = $"{expectedEndpointId}{MessageQueueIdHelper.MessageQueueIdDelimiter}{expectedPriority}";

            var (endpointId, priority) = MessageQueueIdHelper.ParseMessageQueueId(messageQueueId);

            Assert.Equal(expectedEndpointId, endpointId);
            Assert.Equal(expectedPriority, priority);
        }

        [Fact]
        [Unit]
        public void TestParseMessageQueueIdWithPriorityAndDelimiterInEndpointId()
        {
            // Endpoint id with delimiter should be ignored
            var expectedEndpointId = $"{Guid.NewGuid()}{MessageQueueIdHelper.MessageQueueIdDelimiter}4321";
            uint expectedPriority = 1234;
            var messageQueueId = $"{expectedEndpointId}{MessageQueueIdHelper.MessageQueueIdDelimiter}{expectedPriority}";

            var (endpointId, priority) = MessageQueueIdHelper.ParseMessageQueueId(messageQueueId);

            Assert.Equal(expectedEndpointId, endpointId);
            Assert.Equal(expectedPriority, priority);
        }

        [Fact]
        [Unit]
        public void TestParseMessageQueueIdWithDefaultPriorityAndInvalidSuffixInEndpointId()
        {
            // Endpoint id with delimiter should be ignored
            var expectedEndpointId = $"{Guid.NewGuid()}{MessageQueueIdHelper.MessageQueueIdDelimiter}4321a";
            var messageQueueId = expectedEndpointId;
            uint expectedPriority = Core.RouteFactory.DefaultPriority;

            var (endpointId, priority) = MessageQueueIdHelper.ParseMessageQueueId(messageQueueId);

            Assert.Equal(expectedEndpointId, endpointId);
            Assert.Equal(expectedPriority, priority);
        }
    }
}
