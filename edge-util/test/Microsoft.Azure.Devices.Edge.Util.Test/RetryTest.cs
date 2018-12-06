// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using System.Threading.Tasks;

    [Unit]
    public class RetryTest
    {
        [Fact]
        public async Task RetryRetriesFuncUntilValidResultIsReturned()
        {
            int counter = 0;
            Func<Task<int>> func = () => Task.FromResult(++counter);
            Func<int, bool> isValid = (val) => val > 3;
            TimeSpan retryInterval = TimeSpan.FromMilliseconds(2);
            int retryCount = 5;

            Option<int> returnedValue = await Retry.Do(func, isValid, null, retryInterval, retryCount);
            Assert.Equal(Option.Some<int>(4), returnedValue);
        }

        [Fact]
        public async Task RetryReturnsNoneIfFuncNeverReturnsValidResult()
        {
            int counter = 0;
            Func<Task<string>> func = () => Task.FromResult((counter++ > 5) ? "Foo" : null);
            Func<string, bool> isValid = (val) => val != null;
            TimeSpan retryInterval = TimeSpan.FromMilliseconds(2);
            int retryCount = 5;

            Option<string> returnedValue = await Retry.Do(func, isValid, null, retryInterval, retryCount);
            Assert.Equal(Option.None<string>(), returnedValue);
        }

        [Fact]
        public async Task RetryWithoutValidFuncReturns1stResult()
        {
            int counter = 0;
            Func<Task<string>> func = () => { ++counter; return Task.FromResult<string>("Foo"); };
            TimeSpan retryInterval = TimeSpan.FromMilliseconds(2);
            int retryCount = 5;

            Option<string> returnedValue = await Retry.Do(func, null, null, retryInterval, retryCount);
            Assert.Equal(Option.Some<string>("Foo"), returnedValue);
            Assert.Equal(1, counter);
        }

        [Fact]
        public async Task RetryWithoutExceptionFuncThrowsIfFuncThrows()
        {
            Func<Task<string>> func = () => throw new InvalidOperationException();
            TimeSpan retryInterval = TimeSpan.FromMilliseconds(2);
            int retryCount = 5;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => Retry.Do(func, null, null, retryInterval, retryCount)
            );
        }

        [Fact]
        public async Task RetryContinuesIfExceptionFuncReturnsTrue()
        {
            int counter = 0;
            Func<Task<string>> func = () => Task.FromResult((counter++ > 3) ? "Foo" : throw new InvalidOperationException());
            Func<Exception, bool> continueOnException = (ex) => true;
            TimeSpan retryInterval = TimeSpan.FromMilliseconds(2);
            int retryCount = 5;

            Option<string> returnedValue = await Retry.Do(func, null, continueOnException, retryInterval, retryCount);
            Assert.Equal(Option.Some<string>("Foo"), returnedValue);
        }

        [Fact]
        public async Task RetryThrowsIfExceptionFuncReturnsFalse()
        {
            Func<Task<string>> func = () => throw new InvalidOperationException();
            Func<Exception, bool> continueOnException = (ex) => false;
            TimeSpan retryInterval = TimeSpan.FromMilliseconds(2);
            int retryCount = 5;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => Retry.Do(func, null, continueOnException, retryInterval, retryCount)
            );
        }
    }
}
