// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Test.Sinks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Sinks;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Moq;
    using Xunit;

    public class RetryingSinkTest
    {
        [Fact, Unit]
        public async Task SmokeTask()
        {
            var factory = new RetryingSinkFactory<int>(new TestSinkFactory<int>(), RetryPolicy.NoRetry);
            ISink<int> sink = await factory.CreateAsync("hub");
            ISinkResult<int> result = await sink.ProcessAsync(1, CancellationToken.None);

            Assert.True(result.IsSuccessful);
            Assert.Equal(new List<int> { 1 }, result.Succeeded);
            Assert.False(result.Failed.Any());
            Assert.False(result.InvalidDetailsList.Any());

            await sink.CloseAsync(CancellationToken.None);
        }

        [Fact, Unit]
        public async Task TestSendCompletes()
        {
            var retryPolicy = new RetryPolicy(new ErrorDetectionStrategy(_ => true), new FixedInterval(3, TimeSpan.FromMilliseconds(10)));
            var items = new[] { 1, 2, 3, 4, 5, 6 };
            var testSink = new PartialFailureSink<int>(new Exception());
            var sink = new RetryingSink<int>(testSink, retryPolicy);
            ISinkResult<int> result = await sink.ProcessAsync(items, CancellationToken.None);
            await sink.CloseAsync(CancellationToken.None);

            Assert.True(result.IsSuccessful);
            Assert.Equal(new List<int>(items), result.Succeeded);
        }

        [Fact, Unit]
        public async Task TestFailure()
        {
            var retryPolicy = new RetryPolicy(new ErrorDetectionStrategy(_ => true), new FixedInterval(2, TimeSpan.FromMilliseconds(10)));
            var items = new[] { 1, 2, 3, 4, 5, 6 };
            var testSink = new PartialFailureSink<int>(new Exception());
            var sink = new RetryingSink<int>(testSink, retryPolicy);
            ISinkResult<int> result = await sink.ProcessAsync(items, CancellationToken.None);

            Assert.False(result.IsSuccessful);
            Assert.Equal(new List<int> { 1, 2, 3, 4, 5 }, result.Succeeded);
            Assert.Equal(new List<int> { 6 }, result.Failed);
            Assert.Equal(new List<InvalidDetails<int>>(), result.InvalidDetailsList);
        }

        [Fact, Unit]
        public async Task TestCancellation()
        {
            var cts = new CancellationTokenSource();
            var retryPolicy = new RetryPolicy(new ErrorDetectionStrategy(_ => true), new FixedInterval(2, TimeSpan.FromMilliseconds(10)));
            var items = new[] { 1, 2, 3, 4, 5, 6 };

            var testSink = new FailedSink<int>(new Exception());
            var sink = new RetryingSink<int>(testSink, retryPolicy);
            Task<ISinkResult<int>> task = sink.ProcessAsync(items, cts.Token);

            cts.Cancel();
            ISinkResult<int> result = await task;

            Assert.False(result.IsSuccessful);
            Assert.Equal(new List<int>(), result.Succeeded);
            Assert.Equal(new List<int> { 1, 2, 3, 4, 5, 6 }, result.Failed);
            Assert.Equal(new List<InvalidDetails<int>>(), result.InvalidDetailsList);
            Assert.True(result.SendFailureDetails.HasValue);
            result.SendFailureDetails.ForEach(sfd => Assert.IsType<TaskCanceledException>(sfd.RawException));
        }

        [Fact, Unit]
        public async Task TestNonTransient()
        {
            var retryPolicy = new RetryPolicy(new ErrorDetectionStrategy(_ => false), new FixedInterval(int.MaxValue, TimeSpan.FromMilliseconds(10)));
            var items = new[] { 1, 2, 3, 4, 5, 6 };

            var testSink = new FailedSink<int>(new Exception("non-transient"));
            var sink = new RetryingSink<int>(testSink, retryPolicy);

            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
            {
                ISinkResult<int> result = await sink.ProcessAsync(items, cts.Token);

                Assert.False(result.IsSuccessful);
                Assert.Equal(new List<int>(), result.Succeeded);
                Assert.Equal(new List<int> { 1, 2, 3, 4, 5, 6 }, result.Failed);
                Assert.Equal(new List<InvalidDetails<int>>(), result.InvalidDetailsList);
                Assert.True(result.SendFailureDetails.HasValue);
                result.SendFailureDetails.ForEach(sfd => Assert.IsType<Exception>(sfd.RawException));
                result.SendFailureDetails.ForEach(sfd => Assert.Equal("non-transient", sfd.RawException.Message));
            }
        }

        [Fact, Unit]
        public async Task TestClose()
        {
            var underlying = new Mock<ISink<int>>();
            var sink = new RetryingSink<int>(underlying.Object, RetryPolicy.NoRetry);
            await sink.CloseAsync(CancellationToken.None);
            underlying.Verify(s => s.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}