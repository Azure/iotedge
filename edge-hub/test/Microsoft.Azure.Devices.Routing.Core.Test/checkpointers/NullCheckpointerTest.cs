// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Checkpointers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Xunit;

    public class NullCheckpointerTest : RoutingUnitTestBase
    {
        [Theory]
        [Unit]
        [MemberData(nameof(TestAdmitDataSource.TestData), MemberType = typeof(TestAdmitDataSource))]
        public void TestAdmit(IMessage message, bool expected)
        {
            using (var checkpointer = new NullCheckpointer())
            {
                bool result = checkpointer.Admit(message);
                Assert.Equal(expected, result);
            }
        }

        [Theory]
        [Unit]
        [MemberData(nameof(TestCommitDataSource.TestData), MemberType = typeof(TestCommitDataSource))]
        public async Task TestCommit(IMessage message)
        {
            using (var checkpointer1 = new NullCheckpointer())
            {
                await checkpointer1.CommitAsync(new[] { message }, new IMessage[] { }, Option.None<DateTime>(), Option.None<DateTime>(), CancellationToken.None);
                Assert.Equal(Checkpointer.InvalidOffset, checkpointer1.Offset);
                await checkpointer1.CloseAsync(CancellationToken.None);
            }
        }

        static IMessage MessageWithOffset(long offset) =>
            new Message(TelemetryMessageSource.Instance, new byte[] { 1, 2, 3 }, new Dictionary<string, string>(), new Dictionary<string, string>(), offset);

        static class TestAdmitDataSource
        {
            static readonly IList<object[]> Data = new List<object[]>
            {
                new object[] { MessageWithOffset(long.MinValue), true },
                new object[] { MessageWithOffset(0L), true },
                new object[] { MessageWithOffset(37L), true },
                new object[] { MessageWithOffset(38L), true },
                new object[] { MessageWithOffset(long.MaxValue), true },

                new object[] { MessageWithOffset(long.MinValue), true },
                new object[] { MessageWithOffset(0L), true },
                new object[] { MessageWithOffset(37L), true },
                new object[] { MessageWithOffset(38L), true },
                new object[] { MessageWithOffset(long.MaxValue), true },
            };

            public static IEnumerable<object[]> TestData => Data;
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
    }
}
