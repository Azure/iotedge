// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class TwinMessageConverterTest
    {
        static TwinProperties CreateTwinProperties(PropertyCollection desired, PropertyCollection reported)
        {
            string json = JsonConvert.SerializeObject(new { desired, reported });
            return JsonConvert.DeserializeObject<TwinProperties>(json);
        }

        public static IEnumerable<object[]> GetTwinData()
        {
            yield return new object[]
            {
                CreateTwinProperties(new PropertyCollection(), new PropertyCollection()),
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
                CreateTwinProperties(
                    new PropertyCollection()
                    {
                        ["name"] = "value",
                        ["$version"] = 1
                    },
                    new PropertyCollection()),
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
                CreateTwinProperties(
                    new PropertyCollection(),
                    new PropertyCollection()
                    {
                        ["name"] = "value",
                        ["$version"] = 1
                    }),
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
                CreateTwinProperties(
                    new PropertyCollection()
                    {
                        ["name"] = "value",
                        ["$version"] = 1
                    },
                    new PropertyCollection()
                    {
                        ["name"] = "value",
                        ["$version"] = 1
                    }),
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
        public void ConvertsTwinMessagesToMqttMessages(TwinProperties twin, string expectedJson)
        {
            EdgeMessage expectedMessage = new EdgeMessage.Builder(expectedJson.ToBody())
                .SetSystemProperties(
                    new Dictionary<string, string>()
                    {
                        [SystemProperties.EnqueuedTime] = string.Empty
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

            var twin = CreateTwinProperties(new PropertyCollection() { ["$version"] = 10 }, new PropertyCollection());
            IMessage message = messageConverter.ToMessage(twin);
            TwinProperties convertedTwin = messageConverter.FromMessage(message);
            Assert.Equal(twin.Desired.Version, convertedTwin.Desired.Version);
        }

        [Fact]
        public void ConvertedMessageHasAnEnqueuedTimeProperty()
        {
            IMessage actualMessage = new TwinMessageConverter().ToMessage(
                CreateTwinProperties(new PropertyCollection(), new PropertyCollection()));
            Assert.InRange(
                DateTime.Parse(actualMessage.SystemProperties[SystemProperties.EnqueuedTime], null, DateTimeStyles.RoundtripKind),
                DateTime.UtcNow.Subtract(new TimeSpan(0, 1, 0)),
                DateTime.UtcNow);
        }
    }
}
