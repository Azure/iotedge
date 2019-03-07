// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Requests
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class PingRequestHandlerTest
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("{}")]
        [InlineData(@"{""foo"":""bar""}")]
        public async Task TestPingRequest(string payload)
        {
            // Arrange/Act
            var pingRequestHandler = new PingRequestHandler();
            Option<string> response = await pingRequestHandler.HandleRequest(Option.Maybe(payload));

            // Assert
            Assert.False(response.HasValue);
        }
    }
}
