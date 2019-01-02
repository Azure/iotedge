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
        public void TryWithNullValueThrowsTest()
        {
            // Act/Assert
            Assert.Throws<ArgumentNullException>(() => Try.Success<string>(null));
            Assert.Throws<ArgumentNullException>(() => new Try<string>((string)null));
        }

        [Fact]
        public void OkWithValueTest()
        {
            // Arrange
            string value = "Foo";
            Try<string> tryVal = Try.Success(value);

            // Act
            Option<string> valueOption = tryVal.Ok();

            // Assert
            Assert.True(valueOption.HasValue);
            Assert.Equal(value, valueOption.OrDefault());
        }

        [Fact]
        public void OkWithExceptionTest()
        {
            // Arrange
            Exception ex = new InvalidOperationException();
            Try<string> tryVal = Try<string>.Failure(ex);

            // Act
            Option<string> valueOption = tryVal.Ok();

            // Assert
            Assert.False(valueOption.HasValue);
            Assert.Equal(ex, tryVal.Exception);
        }
    }
}
