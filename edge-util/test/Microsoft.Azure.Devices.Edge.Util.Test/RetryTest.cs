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
        public async Task BasicTest()
        {
            int counter = 0;
            Func<Task<Option<int>>> func = () => Task.FromResult((counter++ > 2) ? Option.Some(counter) : Option.None<int>());
            Func<Option<int>, bool> isValid = (option) => option.HasValue;
            Func<Exception, bool> continueOnException = (ex) => true;
            TimeSpan retryInterval = TimeSpan.FromMilliseconds(2);
            int retryCount = 5;

            Option<int> returnedValue = await Retry.Do(func, isValid, continueOnException, retryInterval, retryCount);
            Assert.True(returnedValue.HasValue);
            Assert.Equal(4, returnedValue.OrDefault());
        }

        [Fact]
        public async Task BasicTestWithException()
        {
            int counter = 0;
            Func<Task<string>> func = () => Task.FromResult((counter++ > 3) ? "Foo" : throw new InvalidOperationException());
            Func<string, bool> isValid = null;
            Func<Exception, bool> continueOnException = null;
            TimeSpan retryInterval = TimeSpan.FromMilliseconds(2);
            int retryCount = 5;

            string returnedValue = await Retry.Do(func, isValid, continueOnException, retryInterval, retryCount);
            Assert.NotNull(returnedValue);
            Assert.Equal("Foo", returnedValue);
        }

        [Fact]
        public async Task ValueNotFoundTest()
        {
            int counter = 0;
            Func<Task<string>> func = () => Task.FromResult((counter++ > 5) ? "Foo" : (string)null);
            Func<string, bool> isValid = (val) => val != null;
            Func<Exception, bool> continueOnException = (ex) => true;
            TimeSpan retryInterval = TimeSpan.FromMilliseconds(2);
            int retryCount = 5;

            string returnedValue = await Retry.Do(func, isValid, continueOnException, retryInterval, retryCount);
            Assert.Null(returnedValue);            
        }

        [Fact]
        public async Task ExceptionTest()
        {
            int counter = 0;
            Func<Task<string>> func = () => { counter++; throw new InvalidOperationException(); };
            Func<string, bool> isValid = null;
            Func<Exception, bool> continueOnException = null;
            TimeSpan retryInterval = TimeSpan.FromMilliseconds(2);
            int retryCount = 5;

            Exception caughtException = null;
            try
            {
                await Retry.Do(func, isValid, continueOnException, retryInterval, retryCount);
            }
            catch(InvalidOperationException ex)
            {
                caughtException = ex;
            }

            Assert.NotNull(caughtException);
            Assert.Equal(5, counter);
        }
    }
}
