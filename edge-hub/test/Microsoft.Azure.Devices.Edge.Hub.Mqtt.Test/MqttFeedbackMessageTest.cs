// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class MqttFeedbackMessageTest
    {
        static readonly EdgeMessage Message1 = new EdgeMessage.Builder(new byte[] { 1, 2, 3 }).SetProperties(new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }).Build();

        [Fact]
        public void TestConstructor()
        {
            var message = new MqttFeedbackMessage(Message1, FeedbackStatus.Complete);

            Assert.Equal(FeedbackStatus.Complete, message.FeedbackStatus);
            Assert.Equal(message.Body, Message1.Body);
            Assert.Equal(message.Properties, Message1.Properties);
            Assert.Equal(message.SystemProperties, Message1.SystemProperties);
        }
    }
}
