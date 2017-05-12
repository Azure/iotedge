using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Xunit;

    public class MqttFeedbackMessageTest
    {
        static readonly MqttMessage Message1 = new MqttMessage.Builder(new byte[] { 1, 2, 3 }).SetProperties(new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }).Build();

        [Fact]
        public void TestConstructor()
        {
            var message = new MqttFeedbackMessage(Message1, FeedbackStatus.Complete);

            Assert.Equal(message.FeedbackStatus, FeedbackStatus.Complete);
            Assert.Equal(message.Body, Message1.Body);
            Assert.Equal(message.Properties, Message1.Properties);
            Assert.Equal(message.SystemProperties, Message1.SystemProperties);
        }
    }
}
