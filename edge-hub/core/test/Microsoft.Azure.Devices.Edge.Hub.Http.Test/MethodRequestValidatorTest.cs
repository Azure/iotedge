// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Unit]
    public class MethodRequestValidatorTest
    {
        public static IEnumerable<object[]> GetValidData()
        {
            yield return new object[] { new MethodRequest("poke", new JRaw("{\"prop\":\"val\"}"), 5, 30) };

            yield return new object[] { new MethodRequest("poke", new JRaw("{\"prop\":\"val\"}"), 300, 300) };

            yield return new object[] { new MethodRequest(new string('1', 100), new JRaw("{\"prop\":\"val\"}"), 300, 300) };
        }

        public static IEnumerable<object[]> GetInvalidData()
        {
            yield return new object[] { new MethodRequest("poke", new JRaw("{\"prop\":\"val\"}"), 3, 30), typeof(ArgumentOutOfRangeException) };

            yield return new object[] { new MethodRequest("poke", new JRaw("{\"prop\":\"val\"}"), 302, 30), typeof(ArgumentOutOfRangeException) };

            yield return new object[] { new MethodRequest("poke", new JRaw("{\"prop\":\"val\"}"), 30, 302), typeof(ArgumentOutOfRangeException) };

            yield return new object[] { new MethodRequest("poke", new JRaw("{\"prop\":\"val\"}"), 30, -1), typeof(ArgumentOutOfRangeException) };

            yield return new object[] { new MethodRequest(null, new JRaw("{\"prop\":\"val\"}"), 30, 30), typeof(ArgumentException) };

            yield return new object[] { new MethodRequest(string.Empty, new JRaw("{\"prop\":\"val\"}"), 30, 30), typeof(ArgumentException) };

            yield return new object[] { new MethodRequest(new string('1', 102), new JRaw("{\"prop\":\"val\"}"), 30, 30), typeof(ArgumentException) };

            TestClass testObj = null;
            for (int i = 0; i < 1000; i++)
            {
                testObj = new TestClass(new string('1', 100), new string('2', 100), testObj);
            }

            var jraw = new JRaw(JsonConvert.SerializeObject(testObj));
            yield return new object[] { new MethodRequest("poke", jraw, 30, 30), typeof(ArgumentException) };
        }

        [Theory]
        [MemberData(nameof(GetValidData))]
        public void ValidateRequestTest(MethodRequest request)
        {
            // Arrange
            IValidator<MethodRequest> methodRequestValidator = new MethodRequestValidator();

            // Act / Assert
            methodRequestValidator.Validate(request);
        }

        [Theory]
        [MemberData(nameof(GetInvalidData))]
        public void ValidateInvalidRequestTest(MethodRequest request, Type expectedExceptionType)
        {
            // Arrange
            IValidator<MethodRequest> methodRequestValidator = new MethodRequestValidator();

            // Act / Assert
            Assert.Throws(expectedExceptionType, () => methodRequestValidator.Validate(request));
        }

        class TestClass
        {
            public TestClass(string prop1, string prop2, TestClass obj)
            {
                this.Prop1 = prop1;
                this.Prop2 = prop2;
                this.NestedObj = obj;
            }

            [JsonProperty("prop1")]
            public string Prop1 { get; }

            [JsonProperty("prop2")]
            public string Prop2 { get; }

            [JsonProperty("obj")]
            public TestClass NestedObj { get; }
        }
    }
}
