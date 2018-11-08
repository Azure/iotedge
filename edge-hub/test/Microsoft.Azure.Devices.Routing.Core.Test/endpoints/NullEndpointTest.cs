// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;

    using Xunit;

    public class NullEndpointTest : RoutingUnitTestBase
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 1 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 3, 1, 2 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } });

        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            var endpoint = new NullEndpoint("endpoint1");
            IProcessor processor = endpoint.CreateProcessor();
            Assert.True(processor.ErrorDetectionStrategy.IsTransient(new Exception()));

            Assert.Equal(endpoint, processor.Endpoint);
            ISinkResult<IMessage> result = await processor.ProcessAsync(new IMessage[] { }, CancellationToken.None);
            Assert.Equal(new IMessage[0], result.Succeeded);

            ISinkResult<IMessage> result2 = await processor.ProcessAsync(new[] { Message1, Message2, Message3 }, CancellationToken.None);
            Assert.Equal(new[] { Message1, Message2, Message3 }, result2.Succeeded);

            var endpoint2 = new NullEndpoint("id2", "name2", "hub2");
            Assert.Equal("id2", endpoint2.Id);
            Assert.Equal("name2", endpoint2.Name);
            Assert.Equal("hub2", endpoint2.IotHubName);
        }
    }
}
