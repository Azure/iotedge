// Copyright (c) Microsoft. All rights reserved.
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
    public class MasterCheckpointerTest : RoutingUnitTestBase
    {
        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            var store = new NullCheckpointStore();
            using (MasterCheckpointer master = await MasterCheckpointer.CreateAsync("checkpointer", store))
            {
                ICheckpointer checkpointer1 = await master.CreateAsync("endpoint1");
                ICheckpointer checkpointer2 = await master.CreateAsync("endpoint2");

                Assert.Equal(Checkpointer.InvalidOffset, master.Offset);
                Assert.Equal(Checkpointer.InvalidOffset, checkpointer1.Offset);
                Assert.Equal(Checkpointer.InvalidOffset, checkpointer2.Offset);
                Assert.False(checkpointer1.HasOutstanding);
                Assert.False(checkpointer2.HasOutstanding);

                checkpointer1.Propose(MessageWithOffset(1));
                Assert.Equal(Checkpointer.InvalidOffset, master.Offset);
                Assert.Equal(Checkpointer.InvalidOffset, checkpointer1.Offset);
                Assert.Equal(Checkpointer.InvalidOffset, checkpointer2.Offset);
                Assert.True(checkpointer1.HasOutstanding);
                Assert.False(checkpointer2.HasOutstanding);

                await checkpointer1.CommitAsync(new[] { MessageWithOffset(1) }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);

                Assert.Equal(1, master.Offset);
                Assert.Equal(1, checkpointer1.Offset);
                Assert.Equal(Checkpointer.InvalidOffset, checkpointer2.Offset);
                Assert.False(checkpointer1.HasOutstanding);
                Assert.False(checkpointer2.HasOutstanding);

                await master.CloseAsync(CancellationToken.None);
            }
        }

        [Fact]
        [Unit]
        public async Task TestNoChildren()
        {
            var store = new NullCheckpointStore();
            MasterCheckpointer master = await MasterCheckpointer.CreateAsync("checkpointer", store);
            Assert.Equal(Checkpointer.InvalidOffset, master.Offset);

            await master.CommitAsync(new[] { MessageWithOffset(3), MessageWithOffset(1) }, new[] { MessageWithOffset(2) }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);
            Assert.Equal(1, master.Offset);
        }

        [Fact]
        [Unit]
        public async Task TestTwoPropose()
        {
            var store = new NullCheckpointStore();
            MasterCheckpointer master = await MasterCheckpointer.CreateAsync("checkpointer", store);

            ICheckpointer checkpointer1 = await master.CreateAsync("endpoint1");
            ICheckpointer checkpointer2 = await master.CreateAsync("endpoint2");

            Assert.Equal(Checkpointer.InvalidOffset, master.Offset);
            Assert.Equal(Checkpointer.InvalidOffset, checkpointer1.Offset);
            Assert.Equal(Checkpointer.InvalidOffset, checkpointer2.Offset);
            Assert.False(checkpointer1.HasOutstanding);
            Assert.False(checkpointer2.HasOutstanding);

            checkpointer1.Propose(MessageWithOffset(1));
            checkpointer2.Propose(MessageWithOffset(2));

            Assert.Equal(Checkpointer.InvalidOffset, master.Offset);
            Assert.Equal(Checkpointer.InvalidOffset, checkpointer1.Offset);
            Assert.Equal(Checkpointer.InvalidOffset, checkpointer2.Offset);
            Assert.True(checkpointer1.HasOutstanding);
            Assert.True(checkpointer2.HasOutstanding);

            await checkpointer2.CommitAsync(new[] { MessageWithOffset(2) }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);

            Assert.Equal(Checkpointer.InvalidOffset, master.Offset);
            Assert.Equal(Checkpointer.InvalidOffset, checkpointer1.Offset);
            Assert.Equal(2, checkpointer2.Offset);
            Assert.True(checkpointer1.HasOutstanding);
            Assert.False(checkpointer2.HasOutstanding);

            await checkpointer1.CommitAsync(new[] { MessageWithOffset(1) }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);
            Assert.Equal(2, master.Offset);
            Assert.Equal(1, checkpointer1.Offset);
            Assert.Equal(2, checkpointer2.Offset);
            Assert.False(checkpointer1.HasOutstanding);
            Assert.False(checkpointer2.HasOutstanding);

            checkpointer1.Propose(MessageWithOffset(4));
            checkpointer2.Propose(MessageWithOffset(3));
            Assert.Equal(2, master.Offset);
            Assert.Equal(1, checkpointer1.Offset);
            Assert.Equal(2, checkpointer2.Offset);
            Assert.True(checkpointer1.HasOutstanding);
            Assert.True(checkpointer2.HasOutstanding);

            await checkpointer1.CloseAsync(CancellationToken.None);
            await checkpointer2.CommitAsync(new[] { MessageWithOffset(3) }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);

            Assert.Equal(3, master.Offset);
            Assert.Equal(1, checkpointer1.Offset);
            Assert.Equal(3, checkpointer2.Offset);
            Assert.True(checkpointer1.HasOutstanding);
            Assert.False(checkpointer2.HasOutstanding);
        }

        [Fact]
        [Unit]
        public async Task TestAdmit()
        {
            var store = new NullCheckpointStore(14);
            MasterCheckpointer master = await MasterCheckpointer.CreateAsync("checkpointer", store);

            Assert.False(master.Admit(MessageWithOffset(10)));
            Assert.True(master.Admit(MessageWithOffset(15)));
        }

        [Fact]
        [Unit]
        public async Task TestOutstanding()
        {
            var store = new NullCheckpointStore(1L);
            MasterCheckpointer master = await MasterCheckpointer.CreateAsync("checkpointer", store);
            ICheckpointer checkpointer1 = await master.CreateAsync("endpoint1");

            // Propose two messages
            IMessage message1 = MessageWithOffset(2);
            IMessage message2 = MessageWithOffset(3);
            checkpointer1.Propose(message2);
            checkpointer1.Propose(message1);

            Assert.Equal(3L, checkpointer1.Proposed);
            Assert.Equal(1L, master.Offset);

            // Commit the first message
            await checkpointer1.CommitAsync(new[] { message1 }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);

            // Ensure the master represents the checkpointed message offset
            Assert.True(checkpointer1.HasOutstanding);
            Assert.Equal(2L, master.Offset);
        }

        static IMessage MessageWithOffset(long offset)
        {
            return new Message(TelemetryMessageSource.Instance, new byte[0], new Dictionary<string, string>(), offset);
        }
    }
}
