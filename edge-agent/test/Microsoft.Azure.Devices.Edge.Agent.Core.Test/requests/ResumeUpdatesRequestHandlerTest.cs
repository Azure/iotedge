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
    public class ResumeUpdatesRequestHandlerTest
    {
        [Fact]
        public async Task ResumeUpdatesTest()
        {
            var cts = new CancellationTokenSource();
            var suspendManager = Mock.Of<ISuspendManager>(MockBehavior.Strict);
            Mock.Get(suspendManager).Setup(m => m.ResumeUpdatesAsync(cts.Token)).Returns(Task.CompletedTask);

            var handler = new ResumeUpdatesRequestHandler(suspendManager);

            Option<string> response = await handler.HandleRequest(Option.None<string>(), cts.Token);

            Assert.False(response.HasValue);
            Mock.Get(suspendManager).VerifyAll();
        }
    }
}
