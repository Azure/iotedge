// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class IotHubConnectionExceptionTest
    {
        [Fact]
        [Unit]
        public void IotHubConnection_ExceptionConstructorTest()
        {
            var iotX = new EdgeHubConnectionException("hello, error!");
            Exception ex = iotX;
            Assert.Equal("hello, error!", ex.Message);
        }

        [Fact]
        [Unit]
        public void IotHubConnection_LayeredExceptionConstructorTest()
        {
            var randomX = new ArgumentException();
            var iotX = new EdgeHubConnectionException(null, randomX);
            Exception ex = iotX;
            Assert.Equal(randomX, ex.InnerException);
        }
    }
}
