// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class TaskExTest
    {
        [Fact]
        [Unit]
        public async Task SmokeTest()
        {
            Task t1 = TaskEx.FromException(new InvalidOperationException("the message"));
            Assert.True(t1.IsFaulted);
            InvalidOperationException e1 = await Assert.ThrowsAsync<InvalidOperationException>(() => t1);
            Assert.Equal("the message", e1.Message);

            Task<int> t2 = TaskEx.FromException<int>(new BadImageFormatException("there's a bad image"));
            Assert.True(t1.IsFaulted);
            BadImageFormatException e2 = await Assert.ThrowsAsync<BadImageFormatException>(() => t2);
            Assert.Equal("there's a bad image", e2.Message);
        }

        [Fact]
        [Unit]
        public async Task WhenCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            await cts.Token.WhenCanceled();
        }
    }
}