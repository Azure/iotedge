// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Xunit;

    public static class StringEx
    {
        public static string RemoveWhitespace(this string input) =>
            new string(input.Where(ch => !char.IsWhiteSpace(ch)).ToArray());

        public static string SingleToDoubleQuotes(this string input) => input.Replace('\'', '"');

        public static byte[] ToBody(this string input) =>
            Encoding.UTF8.GetBytes(input.RemoveWhitespace().SingleToDoubleQuotes());
    }

    [Unit]
    public class TwinMessageConverterTest
    {
        public static IEnumerable<object[]> GetTwinData()
        {
            yield return new object[]
            {
                new Twin(),
                @"
                {
                  'desired': {
                  },
                  'reported': {
                  }
                }"
            };

            yield return new object[]
            {
                new Twin()
                {
                    Properties = new TwinProperties()
                    {
                        Desired = new TwinCollection()
                        {
                            ["name"] = "value",
                            ["$version"] = 1
                        }
                    }
                },
                @"
                {
                  'desired': {
                    'name': 'value',
                    '$version': 1
                  },
                  'reported': {
                  }
                }"
            };

            yield return new object[]
            {
                new Twin()
                {
                    Properties = new TwinProperties()
                    {
                        Reported = new TwinCollection()
                        {
                            ["name"] = "value",
                            ["$version"] = 1
                        }
                    }
                },
                @"
                {
                  'desired': {
                  },
                  'reported': {
                    'name': 'value',
                    '$version': 1
                  }
                }"
            };

            yield return new object[]
            {
                new Twin()
                {
                    Properties = new TwinProperties()
                    {
                        Desired = new TwinCollection()
                        {
                            ["name"] = "value",
                            ["$version"] = 1
                        },
                        Reported = new TwinCollection()
                        {
                            ["name"] = "value",
                            ["$version"] = 1
                        }
                    }
                },
                @"
                {
                  'desired': {
                    'name': 'value',
                    '$version': 1
                  },
                  'reported': {
                    'name': 'value',
                    '$version': 1
                  }
                }"
            };
        }

        [Theory]
        [MemberData(nameof(GetTwinData))]
        public void ConvertsTwinMessagesToMqttMessages(Twin twin, string expectedJson)
        {
            MqttMessage expectedMessage = new MqttMessage.Builder(expectedJson.ToBody()).Build();
            IMessage actualMessage = new TwinMessageConverter().ToMessage(twin);
            Assert.Equal(expectedMessage, actualMessage);
        }

        [Fact]
        public void ConvertedMessageHasAnEnqueuedTimeProperty()
        {
            IMessage actualMessage = new TwinMessageConverter().ToMessage(new Twin());
            Assert.InRange(DateTime.Parse(actualMessage.SystemProperties[Core.SystemProperties.EnqueuedTime]),
                DateTime.UtcNow.Subtract(new TimeSpan(0, 1, 0)), DateTime.UtcNow);
        }
    }
}
