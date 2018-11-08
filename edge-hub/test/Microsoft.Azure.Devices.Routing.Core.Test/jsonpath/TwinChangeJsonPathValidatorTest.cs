// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.JsonPath
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Query.JsonPath;

    using Xunit;

    public class TwinChangeJsonPathValidatorTest
    {
        [Theory]
        [Unit]
        [InlineData("")]
        [InlineData("123abc")]
        [InlineData("string with spaces")]
        [InlineData("properties")]
        [InlineData("properties.desired123")]
        [InlineData("properties.reported")]
        [InlineData("properties.reported.ROOT[].LEVEL")]
        public void TwinChangeJsonPathValidator_Failure(string jsonPath)
        {
            string errorDetails;
            Assert.False(TwinChangeJsonPathValidator.IsSupportedJsonPath(jsonPath, out errorDetails));
            Assert.NotEmpty(errorDetails);
        }

        [Theory]
        [Unit]
        [InlineData("properties.reported.property1")]
        [InlineData("properties.desired.property1")]
        [InlineData("properties.reported.root.level1")]
        [InlineData("properties.reported.ROOT.LEVEL")]
        [InlineData("tags.root.level1.level2.level3.level4")]
        public void TwinChangeJsonPathValidator_Success(string jsonPath)
        {
            Assert.True(TwinChangeJsonPathValidator.IsSupportedJsonPath(jsonPath, out _));
        }
    }
}
