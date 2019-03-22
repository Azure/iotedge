// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test.Stream
{
    using Microsoft.Azure.Devices.Edge.Agent.Core.Logs;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.Stream;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class StreamRequestHandlerProviderTest
    {
        [Fact]
        public void TryGetHandlerTest()
        {
            // Arrange
            var logsProvider = Mock.Of<ILogsProvider>();
            var streamRequestHandlerProvider = new StreamRequestHandlerProvider(logsProvider);

            // Act
            bool result = streamRequestHandlerProvider.TryGetHandler("Logs", out IStreamRequestHandler handler);

            // Assert
            Assert.True(result);
            Assert.NotNull(handler);

            // Act
            handler = null;
            result = streamRequestHandlerProvider.TryGetHandler("logs", out handler);

            // Assert
            Assert.True(result);
            Assert.NotNull(handler);

            // Act
            handler = null;
            result = streamRequestHandlerProvider.TryGetHandler("log", out handler);

            // Assert
            Assert.False(result);
            Assert.Null(handler);

            // Act
            handler = null;
            result = streamRequestHandlerProvider.TryGetHandler("foo", out handler);

            // Assert
            Assert.False(result);
            Assert.Null(handler);
        }
    }
}
