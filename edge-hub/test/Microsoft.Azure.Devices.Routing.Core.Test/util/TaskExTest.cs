// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Util
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Xunit;

    public class TaskExTest
    {
        [Fact, Unit]
        public async Task SmokeTest()
        {
            Task t1 = TaskEx.FromException(new ApplicationException("the message"));
            Assert.True(t1.IsFaulted);
            ApplicationException e1 = await Assert.ThrowsAsync<ApplicationException>(() => t1);
            Assert.Equal("the message", e1.Message);

            Task<int> t2 = TaskEx.FromException<int>(new BadImageFormatException("there's a bad image"));
            Assert.True(t1.IsFaulted);
            BadImageFormatException e2 = await Assert.ThrowsAsync<BadImageFormatException>(() => t2);
            Assert.Equal("there's a bad image", e2.Message);
        }

        [Fact, Unit]
        public async Task WhenCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            await cts.Token.WhenCanceled();
        }
    }
}