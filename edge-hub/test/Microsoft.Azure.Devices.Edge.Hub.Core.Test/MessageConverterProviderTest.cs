// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class MessageConverterProviderTest
    {
        class TestMessageConverter : IMessageConverter<int>
        {
            public IMessage ToMessage(int sourceMessage)
            {
                throw new NotImplementedException();
            }

            public int FromMessage(IMessage message)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void ProvidesAConverterForTheGivenType()
        {
            var expectedConverter = new TestMessageConverter();
            var provider = new MessageConverterProvider(
                new Dictionary<Type, IMessageConverter>()
                {
                    { typeof(int), expectedConverter }
                });

            IMessageConverter<int> actualConverter = provider.Get<int>();
            Assert.Same(expectedConverter, actualConverter);
        }

        [Fact]
        public void ThrowsWhenItDoesNotHaveTheRequestedProvider()
        {
            var provider = new MessageConverterProvider(new Dictionary<Type, IMessageConverter>());
            var fn = new Func<IMessageConverter<int>>(() => provider.Get<int>());
            Assert.Throws<KeyNotFoundException>(fn);
        }
    }
}
