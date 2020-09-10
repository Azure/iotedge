// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Unit]
    public class MethodRequestTest
    {
        public static IEnumerable<object[]> GetMethodRequestData()
        {
            yield return new object[]
            {
                @"{
                  ""methodName"": ""command"",
                  ""payload"": {
                    ""prop1"": ""value1""
                  },
                  ""responseTimeoutInSeconds"": 60,
                  ""connectTimeoutInSeconds"": 60
                }",
                new MethodRequest("command", new JRaw("{\"prop1\":\"value1\"}"), 60, 60)
            };

            yield return new object[]
            {
                @"{
                  ""methodName"": ""command"",
                  ""payload"": {
                    ""prop1"": ""value1""
                  },
                  ""responseTimeoutInSeconds"": 60
                }",
                new MethodRequest("command", new JRaw("{\"prop1\":\"value1\"}"), 60, 0)
            };

            yield return new object[]
            {
                @"{
                  ""methodName"": ""command"",
                  ""payload"": {
                    ""prop1"": ""value1""
                  },
                  ""connectTimeoutInSeconds"": 60
                }",
                new MethodRequest("command", new JRaw("{\"prop1\":\"value1\"}"), 30, 60)
            };

            yield return new object[]
            {
                @"{
                  ""methodName"": ""command"",
                  ""payload"": {
                    ""prop1"": ""value1""
                  }
                }",
                new MethodRequest("command", new JRaw("{\"prop1\":\"value1\"}"), 30, 0)
            };
        }

        [Theory]
        [MemberData(nameof(GetMethodRequestData))]
        public void DeserializationTest(string input, MethodRequest expectedMethodRequest)
        {
            // Act
            var methodRequest = JsonConvert.DeserializeObject<MethodRequest>(input);

            // Assert
            Assert.NotNull(methodRequest);
            Assert.Equal(expectedMethodRequest.MethodName, methodRequest.MethodName);
            Assert.Equal(expectedMethodRequest.Payload, methodRequest.Payload);
            Assert.Equal(expectedMethodRequest.ConnectTimeout, methodRequest.ConnectTimeout);
            Assert.Equal(expectedMethodRequest.ResponseTimeout, methodRequest.ResponseTimeout);
        }
    }
}
