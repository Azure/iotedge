// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Checkpointers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    using Xunit;

    [ExcludeFromCodeCoverage]
    public class LoggedCheckpointTest : RoutingUnitTestBase
    {
        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3, 4 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 1L);
        static readonly IMessage Message2 = new Message(TelemetryMessageSource.Instance, new byte[] { 2, 3, 4, 1 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 2L);
        static readonly IMessage Message3 = new Message(TelemetryMessageSource.Instance, new byte[] { 3, 4, 1, 2 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 3L);
        static readonly IMessage Message4 = new Message(TelemetryMessageSource.Instance, new byte[] { 4, 1, 2, 3 }, new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }, 4L);

        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            using (var checkpointer = new LoggedCheckpointer(new NullCheckpointer()))
            {
                Assert.Equal(Checkpointer.InvalidOffset, checkpointer.Offset);
                Assert.True(checkpointer.Admit(Message1));
                await checkpointer.CommitAsync(new[] { Message3, Message4 }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);
                await checkpointer.CommitAsync(new[] { Message1, Message2 }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);

                Assert.Equal(new List<IMessage> { Message3, Message4, Message1, Message2 }, checkpointer.Processed);
                Assert.Equal(Checkpointer.InvalidOffset, checkpointer.Offset);
                await checkpointer.CloseAsync(CancellationToken.None);
            }
        }
    }
}
