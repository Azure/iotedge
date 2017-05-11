// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    public class MessageQueryTest : RoutingUnitTestBase
    {
        const string MessageBody =
            @"{
               ""message"": {
                  ""Weather"": {
                     ""Temperature"": 50,
                     ""Location"": {
                        ""Street"": ""One Microsoft Way"",
                        ""City"": ""Redmond"",
                        ""State"": ""WA""
                                }
                  }
               }
            }";

        static readonly IMessage BodyQueryMessage_Utf8_ValidJson = new Message(MessageSource.Telemetry,
            Encoding.UTF8.GetBytes(MessageBody),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "UTF-8" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessage_Utf16_ValidJson = new Message(MessageSource.Telemetry,
            Encoding.Unicode.GetBytes(MessageBody),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "UTf-16" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessage_Utf32_ValidJson = new Message(MessageSource.Telemetry,
            Encoding.UTF32.GetBytes(MessageBody),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "utf-32" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessage_Utf8_InvalidJson = new Message(MessageSource.Telemetry,
            Encoding.UTF8.GetBytes("Invalid Json"),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "UTF-8" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessage_InvalidEncoding = new Message(MessageSource.Telemetry,
            new byte[] { 1, 2, 3 },
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "UTF-8" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessage_Utf8_ValidJson_MissingEncodingProperty = new Message(MessageSource.Telemetry,
            Encoding.UTF8.GetBytes(MessageBody),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        static readonly IMessage BodyQueryMessage_Utf8_ValidJson_MissingContentTypeProperty = new Message(MessageSource.Telemetry,
            Encoding.UTF8.GetBytes(MessageBody),
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { SystemProperties.ContentEncoding, "Utf-8" },
            });

        [Fact, Unit]
        public void TestMessageQuery()
        {
            Assert.Equal(new QueryValue(50, QueryValueType.Double),
                BodyQueryMessage_Utf8_ValidJson.GetQueryValue("message.Weather.Temperature"));

            Assert.Equal(new QueryValue(50, QueryValueType.Double),
                BodyQueryMessage_Utf16_ValidJson.GetQueryValue("message.Weather.Temperature"));

            Assert.Equal(new QueryValue(50, QueryValueType.Double),
                BodyQueryMessage_Utf32_ValidJson.GetQueryValue("message.Weather.Temperature"));

            Assert.Throws(typeof(JsonReaderException),
                () => BodyQueryMessage_Utf8_InvalidJson.GetQueryValue("message.Weather.Temperature"));

            Assert.Throws(typeof(JsonReaderException),
                () => BodyQueryMessage_InvalidEncoding.GetQueryValue("message.Weather.Temperature"));

            Assert.Throws(typeof(InvalidOperationException),
                () => BodyQueryMessage_Utf8_ValidJson_MissingEncodingProperty.GetQueryValue("message.Weather.Temperature"));

            Assert.Throws(typeof(InvalidOperationException),
                () => BodyQueryMessage_Utf8_ValidJson_MissingContentTypeProperty.GetQueryValue("message.Weather.Temperature"));
        }
    }
}
