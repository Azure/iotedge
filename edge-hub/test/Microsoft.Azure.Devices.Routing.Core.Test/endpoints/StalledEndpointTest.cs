// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Xunit;

    public class StalledEndpointTest : RoutingUnitTestBase
    {
        [Fact]
        [Unit]
        public void SmokeTest()
        {
            var endpoint = new StalledEndpoint("id1");
            IProcessor processor = endpoint.CreateProcessor();

            Assert.Equal(endpoint, processor.Endpoint);
            Assert.True(processor.ErrorDetectionStrategy.IsTransient(new Exception()));
            Assert.Equal(string.Empty, endpoint.IotHubName);

            var cts = new CancellationTokenSource();
            Task<ISinkResult<IMessage>> result = processor.ProcessAsync(new IMessage[] { }, cts.Token);
            Assert.False(result.IsCompleted);
            Assert.False(result.IsCanceled);
            Assert.False(result.IsFaulted);

            cts.Cancel();
            Assert.True(result.IsCompleted);
            Assert.True(result.IsCanceled);
            Assert.False(result.IsFaulted);
        }
    }
}
