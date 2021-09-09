// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.JsonPath
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Query.JsonPath;
    using Xunit;

    public class JsonPathValidatorTest
    {
        [Theory]
        [Unit]
        [InlineData("root")]
        [InlineData("ROOT")]
        [InlineData("root.level1")]
        [InlineData("ROOT.LEVEL1")]
        [InlineData("root.level1.level2.level3.level4")]
        [InlineData("root.level1[0]")]
        [InlineData("root.level1[0].level2[1].level3[3].level4[5]")]
        [InlineData("root[10000]")]
        [InlineData("123abc")]
        public void JsonPathValidator_Success(string jsonPath)
        {
            Assert.True(JsonPathValidator.IsSupportedJsonPath(jsonPath, out _));
        }

        [Theory]
        [Unit]
        [InlineData("")]
        [InlineData("root.")]
        [InlineData("ROOT[")]
        [InlineData("root.level1.")]
        [InlineData("root.level1..level2")]
        [InlineData("ROOT.LEVEL1.")]
        [InlineData("root.level1[")]
        [InlineData("root.level1[]")]
        [InlineData("root.level1['test']")]
        [InlineData("root.level1[\"test\"]")]
        [InlineData("root.level1[].level2[1].level3[3].level4[5]")]
        [InlineData("root[10000].level1.[1]")]
        [InlineData("root[10000].level1[1].level2[]")]
        [InlineData("       test root[10000].level1[1].level2[]")]
        public void JsonPathValidator_Fail(string jsonPath)
        {
            string errorDetails;
            Assert.False(JsonPathValidator.IsSupportedJsonPath(jsonPath, out errorDetails));
            Assert.NotEmpty(errorDetails);
        }

        [Fact]
        [Unit]
        public void JsonPathValidator_Debug()
        {
            string jsonPath = "message.Weather.HistoricalData[0].Temperature[1]";

            bool isSupported = JsonPathValidator.IsSupportedJsonPath(jsonPath, out _);

            Assert.True(isSupported);
        }
    }
}
