// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class TryTests
    {
        [Fact]
        public void OkWithValueTest()
        {
            // Arrange
            string value = "Foo";
            var tryVal = Try.Success(value);

            // Act
            var valueOption = tryVal.Ok();

            // Assert
            Assert.True(valueOption.HasValue);
            Assert.Equal(value, valueOption.OrDefault());
        }

        [Fact]
        public void OkWithExceptionTest()
        {
            // Arrange
            Exception ex = new InvalidOperationException();
            var tryVal = Try.Failure<string>(ex);

            // Act
            Option<string> valueOption = tryVal.Ok();

            // Assert
            Assert.False(valueOption.HasValue);
            Assert.Equal(ex, tryVal.Exception);
        }
    }
}
