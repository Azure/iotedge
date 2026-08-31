// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Requests
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Requests;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class SuspendUpdatesRequestHandlerTest
    {
        [Fact]
        public async Task SuspendUpdatesTest()
        {
            var cts = new CancellationTokenSource();
            var suspendManager = Mock.Of<ISuspendManager>(MockBehavior.Strict);
            Mock.Get(suspendManager).Setup(m => m.SuspendUpdatesAsync(cts.Token)).Returns(Task.CompletedTask);

            var handler = new SuspendUpdatesRequestHandler(suspendManager);

            Option<string> response = await handler.HandleRequest(Option.None<string>(), cts.Token);

            Assert.False(response.HasValue);
            Mock.Get(suspendManager).VerifyAll();
        }
    }
}
