// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Checkpointers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class NullCheckpointerTest : RoutingUnitTestBase
    {
        static IMessage MessageWithOffset(long offset) =>
            new Message(MessageSource.Telemetry, new byte[] { 1, 2, 3 }, new Dictionary<string, string>(), new Dictionary<string, string>(), offset);

        static class TestAdmitDataSource
        {
            static readonly IList<object[]> Data = new List<object[]>
            {
                new object[] { MessageWithOffset(long.MinValue), 37L, true },
                new object[] { MessageWithOffset(0L), 37L, true },
                new object[] { MessageWithOffset(37L), 37L, true },
                new object[] { MessageWithOffset(38L), 37L, true },
                new object[] { MessageWithOffset(long.MaxValue), 37L, true },

                new object[] { MessageWithOffset(long.MinValue), 0L, true },
                new object[] { MessageWithOffset(0L), 0L, true },
                new object[] { MessageWithOffset(37L), 0L, true },
                new object[] { MessageWithOffset(38L), 0L, true },
                new object[] { MessageWithOffset(long.MaxValue), 0L, true },
            };

            public static IEnumerable<object[]> TestData => Data;
        }

        [Theory, Unit]
        [MemberData(nameof(TestAdmitDataSource.TestData), MemberType = typeof(TestAdmitDataSource))]
        public void TestAdmit(IMessage message, long offset, bool expected)
        {
            using (var checkpointer = new NullCheckpointer())
            {
                bool result = checkpointer.Admit(message);
                Assert.Equal(expected, result);
            }
        }

        static class TestCommitDataSource
        {
            static readonly IList<object[]> Data = new List<object[]>
            {
                new object[] { MessageWithOffset(30) },
                new object[] { MessageWithOffset(1000) },
                new object[] { MessageWithOffset(1001) },
                new object[] { MessageWithOffset(long.MaxValue) },
                new object[] { MessageWithOffset(long.MinValue) },
            };

            public static IEnumerable<object[]> TestData => Data;
        }

        [Theory, Unit]
        [MemberData(nameof(TestCommitDataSource.TestData), MemberType = typeof(TestCommitDataSource))]
        public async Task TestCommit(IMessage message)
        {
            using (var checkpointer1 = new NullCheckpointer())
            {
                await checkpointer1.CommitAsync(new[] { message }, new IMessage[] {}, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);
                Assert.Equal(Checkpointer.InvalidOffset, checkpointer1.Offset);
                await checkpointer1.CloseAsync(CancellationToken.None);
            }
        }
    }
}