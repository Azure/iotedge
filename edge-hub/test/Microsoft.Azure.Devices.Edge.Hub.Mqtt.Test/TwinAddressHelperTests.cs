// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Primitives;
    using Xunit;

    [Unit]
    public class TwinAddressHelperTests
    {
        [Theory]
        [InlineData("$iothub/twin/GET/?$rid=6f2c8", true, TwinAddressHelper.Operation.TwinGetState, "", new[] { "$rid", "6f2c8" })]
        [InlineData("$iothub/twin/PATCH/properties/reported/?$rid=5de34", true, TwinAddressHelper.Operation.TwinPatchReportedState, "", new[] { "$rid", "5de34" })]
        [InlineData("$iothub/twin/PATCH/properties/reported/?$rid=5de34&$version=1", true, TwinAddressHelper.Operation.TwinPatchReportedState, "", new[] { "$rid", "5de34", "$version", "1" })]
        [InlineData("$iothub/methods/res/200/?$rid=1", true, TwinAddressHelper.Operation.DirectMethodResponse, "200", new[] { "$rid", "1" })]
        [InlineData("$iothub/methods/res/200/?$rid=1&$random", true, TwinAddressHelper.Operation.DirectMethodResponse, "200", new[] { "$rid", "1", "$random", "" })]
        [InlineData("$iothub/methods/res/200/?$rid=1&$random&$version=2", true, TwinAddressHelper.Operation.DirectMethodResponse, "200", new[] { "$rid", "1", "$random", "", "$version", "2" })]
        [InlineData("$iothub/twin/PATCH/properties/reported/?$rid=错误&$version=%20a", true, TwinAddressHelper.Operation.TwinPatchReportedState, "", new[] { "$rid", "错误", "$version", "%20a" })]
        [InlineData("$iothub/methods/res/200/?$rid=1&random", true, TwinAddressHelper.Operation.DirectMethodResponse, "200", new[] { "$rid", "1", "random", "" })]
        [InlineData("$iothub/methods/res/200/?$rid=1&random=", true, TwinAddressHelper.Operation.DirectMethodResponse, "200", new[] { "$rid", "1", "random", "" })]
        [InlineData("$iothub/methods/res/200/?$rid=1&", true, TwinAddressHelper.Operation.DirectMethodResponse, "200", new[] { "$rid", "1" })]
        [InlineData("$iothub/methods/res/200/?$rid=1&=", true, TwinAddressHelper.Operation.DirectMethodResponse, "200", new[] { "$rid", "1", "", "" })]
        [InlineData("$iothub/methods/res/200/?$rid=1&=value", true, TwinAddressHelper.Operation.DirectMethodResponse, "200", new[] { "$rid", "1", "", "value" })]
        public void TwinTryParseOperationTests(string input, bool expectedOutcome, TwinAddressHelper.Operation expectedOperation,
            string expectedSubresource, string[] expectedProperties)
        {
            var properties = new Dictionary<StringSegment, StringSegment>();
            TwinAddressHelper.Operation operation;
            StringSegment subresource;
            Assert.Equal(expectedOutcome, TwinAddressHelper.TryParseOperation(input, properties, out operation, out subresource));
            if (!expectedOutcome)
            {
                return;
            }

            Assert.Equal(expectedOperation, operation);
            Assert.Equal(expectedSubresource, subresource.ToString());
            Dictionary<StringSegment, StringSegment> expectedPropertyMap = ComposeMapFromPairs(expectedProperties, k => new StringSegment(k), v => new StringSegment(v));
            Assert.Equal(expectedPropertyMap, properties);
        }

        [Theory]
        [InlineData("123", "test", "$iothub/methods/POST/test/?$rid=123")]
        public void TwinFormatDirectRequestTopic(string correlationId, string methodName, string expectedResult)
        {
            Assert.Equal(expectedResult, TwinAddressHelper.FormatDeviceMethodRequestAddress(correlationId, methodName));
        }

        static Dictionary<TKey, TValue> ComposeMapFromPairs<T, TKey, TValue>(T[] pairs, Func<T, TKey> keyFunc, Func<T, TValue> valueFunc)
        {
            var expectedPropertyMap = new Dictionary<TKey, TValue>();
            for (int i = 0; i < pairs.Length; i += 2)
            {
                expectedPropertyMap.Add(keyFunc(pairs[i]), valueFunc(pairs[i + 1]));
            }
            return expectedPropertyMap;
        }
    }
}
