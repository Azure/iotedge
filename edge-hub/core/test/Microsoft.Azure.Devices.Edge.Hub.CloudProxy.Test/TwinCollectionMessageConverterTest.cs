// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class PropertyCollectionMessageConverterTest
    {
        public static IEnumerable<object[]> GetPropertyCollectionData()
        {
            yield return new object[]
            {
                new PropertyCollection(),
                @"
                {
                }"
            };

            yield return new object[]
            {
                new PropertyCollection()
                {
                    ["name"] = "value",
                    ["$version"] = 33
                },
                @"
                {
                  'name': 'value',
                  '$version': 33
                }"
            };

            yield return new object[]
            {
                new PropertyCollection()
                {
                    ["one"] = new PropertyCollection()
                    {
                        ["level"] = 1,
                        ["two"] = new PropertyCollection()
                        {
                            ["level"] = 2,
                            ["three"] = new PropertyCollection()
                            {
                                ["level"] = 3
                            }
                        }
                    },
                    ["$version"] = 6
                },
                @"
                {
                  'one': {
                    'level': 1,
                    'two': {
                      'level': 2,
                      'three': {
                        'level': 3
                      }
                    }
                  },
                  '$version': 6
                }"
            };
        }

        [Theory]
        [MemberData(nameof(GetPropertyCollectionData))]
        public void ConvertsPropertyCollectionsToMqttMessages(PropertyCollection collection, string expectedJson)
        {
            EdgeMessage expectedMessage = new EdgeMessage.Builder(expectedJson.ToBody())
                .SetSystemProperties(
                    new Dictionary<string, string>()
                    {
                        [SystemProperties.EnqueuedTime] = string.Empty,
                        [SystemProperties.Version] = collection.Version.ToString()
                    })
                .Build();
            IMessage actualMessage = new PropertyCollectionMessageConverter().ToMessage(collection);
            Assert.Equal(expectedMessage.Body, actualMessage.Body);
            Assert.Equal(expectedMessage.Properties, actualMessage.Properties);
            Assert.Equal(expectedMessage.SystemProperties.Keys, actualMessage.SystemProperties.Keys);
            Assert.Equal(expectedMessage.SystemProperties[SystemProperties.Version], actualMessage.SystemProperties[SystemProperties.Version]);
        }

        [Fact]
        public void ConvertedMessageHasAnEnqueuedTimeProperty()
        {
            IMessage actualMessage = new PropertyCollectionMessageConverter().ToMessage(new PropertyCollection());
            Assert.InRange(
                DateTime.Parse(actualMessage.SystemProperties[SystemProperties.EnqueuedTime], null, DateTimeStyles.RoundtripKind),
                DateTime.UtcNow.Subtract(new TimeSpan(0, 1, 0)),
                DateTime.UtcNow);
        }
    }
}
