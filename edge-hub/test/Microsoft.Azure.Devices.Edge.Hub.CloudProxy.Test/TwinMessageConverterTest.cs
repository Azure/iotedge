// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Xunit;

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
            EdgeMessage expectedMessage = new EdgeMessage.Builder(expectedJson.ToBody())
                .SetSystemProperties(new Dictionary<string, string>()
                {
                    [SystemProperties.EnqueuedTime] = ""
                })
                .Build();
            IMessage actualMessage = new TwinMessageConverter().ToMessage(twin);
            Assert.Equal(expectedMessage.Body, actualMessage.Body);
            Assert.Equal(expectedMessage.Properties, actualMessage.Properties);
            Assert.Equal(expectedMessage.SystemProperties.Keys, actualMessage.SystemProperties.Keys);
        }

        [Fact]
        public void CheckVersionConvertion()
        {
            var messageConverter = new TwinMessageConverter();

            var twin = new Twin("d1") { Version = 10 };
            IMessage message = messageConverter.ToMessage(twin);
            Twin convertedTwin = messageConverter.FromMessage(message);
            Assert.Equal(twin.Version, convertedTwin.Version);
        }

        [Fact]
        public void ConvertedMessageHasAnEnqueuedTimeProperty()
        {
            IMessage actualMessage = new TwinMessageConverter().ToMessage(new Twin());
            Assert.InRange(
                DateTime.Parse(actualMessage.SystemProperties[SystemProperties.EnqueuedTime], null, DateTimeStyles.RoundtripKind),
                DateTime.UtcNow.Subtract(new TimeSpan(0, 1, 0)), DateTime.UtcNow);
        }
    }
}
