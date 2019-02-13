// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Checkpointers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Moq;
    using Xunit;

    public class CheckpointerTest : RoutingUnitTestBase
    {
        [Theory]
        [Unit]
        [MemberData(nameof(TestAdmitDataSource.TestData), MemberType = typeof(TestAdmitDataSource))]
        public async Task TestAdmit(Message message, long offset, bool expected)
        {
            var store = new Mock<ICheckpointStore>();
            store.Setup(d => d.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(offset));

            using (ICheckpointer checkpointer = await Checkpointer.CreateAsync("id1", store.Object))
            {
                bool result = checkpointer.Admit(message);
                Assert.Equal(expected, result);
                await checkpointer.CloseAsync(CancellationToken.None);
            }
        }

        [Theory]
        [Unit]
        [MemberData(nameof(TestCommitDataSource.TestData), MemberType = typeof(TestCommitDataSource))]
        public async Task TestCommit(Message message, long offset, long expected)
        {
            var store = new Mock<ICheckpointStore>();
            store.Setup(d => d.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(offset));

            using (ICheckpointer checkpointer1 = await Checkpointer.CreateAsync("id1", store.Object))
            {
                await checkpointer1.CommitAsync(new IMessage[] { message }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);
                Assert.Equal(expected, checkpointer1.Offset);
                Expression<Func<CheckpointData, bool>> expr = obj => obj.Offset == expected;
                if (message.Offset == expected && message.Offset != offset)
                {
                    // this is a valid commit and the store should update
                    store.Verify(d => d.SetCheckpointDataAsync("id1", It.Is(expr), It.IsAny<CancellationToken>()), Times.Exactly(1));
                }
                else
                {
                    store.Verify(d => d.SetCheckpointDataAsync(It.IsAny<string>(), new CheckpointData(It.IsAny<long>()), It.IsAny<CancellationToken>()), Times.Never);
                }

                await checkpointer1.CloseAsync(CancellationToken.None);
            }
        }

        [Fact]
        [Unit]
        public async Task TestCommitMultiple()
        {
            var store = new Mock<ICheckpointStore>();
            store.Setup(d => d.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(10));

            using (ICheckpointer checkpointer1 = await Checkpointer.CreateAsync("id1", store.Object))
            {
                IMessage[] tocheckpoint = new[] { MessageWithOffset(13), MessageWithOffset(12), MessageWithOffset(11), MessageWithOffset(10), MessageWithOffset(9) };
                await checkpointer1.CommitAsync(tocheckpoint, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);
                Assert.Equal(13, checkpointer1.Offset);
                await checkpointer1.CloseAsync(CancellationToken.None);
            }
        }

        [Theory]
        [Unit]
        [MemberData(nameof(TestCommitRemainingDataSource.TestData), MemberType = typeof(TestCommitRemainingDataSource))]
        public async Task TestCommitWithRemaining(IMessage[] successful, IMessage[] remaining)
        {
            var store = new Mock<ICheckpointStore>();
            store.Setup(d => d.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(-1));

            ICheckpointer checkpointer = await Checkpointer.CreateAsync("id1", store.Object);
            await checkpointer.CommitAsync(successful, remaining, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);

            Assert.True(remaining.All(m => checkpointer.Admit(m)));
        }

        [Fact]
        [Unit]
        public async Task TestConstructor()
        {
            var store = new Mock<ICheckpointStore>();
            store.Setup(d => d.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(10));

            await Assert.ThrowsAsync<ArgumentNullException>(() => Checkpointer.CreateAsync(null, store.Object));
            await Assert.ThrowsAsync<ArgumentNullException>(() => Checkpointer.CreateAsync("id", null));
            await Assert.ThrowsAsync<ArgumentNullException>(() => Checkpointer.CreateAsync(null, null));
        }

        [Fact]
        [Unit]
        public async Task TestClose()
        {
            var store = new Mock<ICheckpointStore>();
            store.Setup(d => d.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(10));

            using (ICheckpointer checkpointer1 = await Checkpointer.CreateAsync("id1", store.Object))
            {
                await checkpointer1.CommitAsync(new[] { MessageWithOffset(20) }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);
                Assert.Equal(20, checkpointer1.Offset);

                await checkpointer1.CloseAsync(CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(() => checkpointer1.CommitAsync(new[] { MessageWithOffset(30) }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None));

                // close a second time
                await checkpointer1.CloseAsync(CancellationToken.None);
                await Assert.ThrowsAsync<InvalidOperationException>(() => checkpointer1.CommitAsync(new[] { MessageWithOffset(40) }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None));
                Assert.Equal(20, checkpointer1.Offset);

                bool result = checkpointer1.Admit(MessageWithOffset(30));
                Assert.False(result);
            }
        }

        [Fact]
        [Unit]
        public async Task TestCancellation()
        {
            var store = new Mock<ICheckpointStore>();
            store.Setup(d => d.GetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new CheckpointData(10));

            using (var cts = new CancellationTokenSource())
            using (ICheckpointer checkpointer1 = await Checkpointer.CreateAsync("id1", store.Object))
            {
                var tcs = new TaskCompletionSource<bool>();
                cts.Token.Register(() => tcs.SetCanceled());

                store.Setup(d => d.SetCheckpointDataAsync(It.IsAny<string>(), It.IsAny<CheckpointData>(), It.IsAny<CancellationToken>())).Returns(tcs.Task);

                Task result = checkpointer1.CommitAsync(new[] { MessageWithOffset(20) }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);

                cts.Cancel();
                await Assert.ThrowsAsync<TaskCanceledException>(() => result);
                Assert.Equal(20, checkpointer1.Offset);

                await checkpointer1.CloseAsync(CancellationToken.None);
            }
        }

        static IMessage MessageWithOffset(long offset) =>
            new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string>(), offset);

        static class TestAdmitDataSource
        {
            static readonly IList<object[]> Data = new List<object[]>
            {
                new object[] { MessageWithOffset(long.MinValue), 37L, false },
                new object[] { MessageWithOffset(0L), 37L, false },
                new object[] { MessageWithOffset(37L), 37L, false },
                new object[] { MessageWithOffset(38L), 37L, true },
                new object[] { MessageWithOffset(long.MaxValue), 37L, true },

                new object[] { MessageWithOffset(long.MinValue), 0L, false },
                new object[] { MessageWithOffset(0L), 0L, false },
                new object[] { MessageWithOffset(37L), 0L, true },
                new object[] { MessageWithOffset(38L), 0L, true },
                new object[] { MessageWithOffset(long.MaxValue), 0L, true },
            };

            public static IEnumerable<object[]> TestData => Data;
        }

        static class TestCommitDataSource
        {
            static readonly IList<object[]> Data = new List<object[]>
            {
                new object[] { MessageWithOffset(30), 1000, 1000 },
                new object[] { MessageWithOffset(1000), 1000, 1000 },
                new object[] { MessageWithOffset(1001), 1000, 1001 },
                new object[] { MessageWithOffset(long.MaxValue), 1000, long.MaxValue },
                new object[] { MessageWithOffset(long.MinValue), 1000, 1000 },
            };

            public static IEnumerable<object[]> TestData => Data;
        }

        static class TestCommitRemainingDataSource
        {
            static readonly IList<object[]> Data = new List<object[]>
            {
                new object[] { new[] { MessageWithOffset(30), MessageWithOffset(31) }, new Message[0] },
                new object[] { new[] { MessageWithOffset(31), MessageWithOffset(30) }, new Message[0] },
                new object[] { new[] { MessageWithOffset(31), MessageWithOffset(29) }, new[] { MessageWithOffset(30) } },
                new object[] { new[] { MessageWithOffset(31), MessageWithOffset(29) }, new[] { MessageWithOffset(30), MessageWithOffset(28) } },
                new object[] { new[] { MessageWithOffset(31), MessageWithOffset(29) }, new[] { MessageWithOffset(28), MessageWithOffset(30) } },
                new object[] { new[] { MessageWithOffset(0), MessageWithOffset(1) }, new[] { MessageWithOffset(2), MessageWithOffset(3) } },
                new object[] { new[] { MessageWithOffset(2), MessageWithOffset(3) }, new[] { MessageWithOffset(0), MessageWithOffset(1) } },
            };

            public static IEnumerable<object[]> TestData => Data;
        }
    }
}
