// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    [Unit]
    public class ProxyTest
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" \t ")]
        public void ParseReturnsNone(string uri)
        {
            var logger = Mock.Of<ILogger>();
            Assert.Equal(Option.None<IWebProxy>(), Proxy.Parse(uri, logger));
        }

        [Theory]
        [InlineData("http://proxyserver:1234", null, null)]
        [InlineData("http://user@proxyserver:1234", "user", "")]
        [InlineData("http://user:password@proxyserver:1234", "user", "password")]
        public void ParseReturnsSome(string uri, string expectedUsername, string expectedPassword)
        {
            var logger = Mock.Of<ILogger>();
            Option<IWebProxy> proxy = Proxy.Parse(uri, logger);
            Assert.True(proxy.HasValue);
            proxy.ForEach(
                p =>
                {
                    Assert.Equal(new Uri(uri), p.GetProxy(new Uri("https://whatever")));
                    NetworkCredential credential = p.Credentials?.GetCredential(new Uri("http://whatever"), string.Empty);
                    Assert.Equal(expectedUsername, credential?.UserName);
                    Assert.Equal(expectedPassword, credential?.Password);
                });
        }

        [Fact]
        public void ParseThrowsOnBadUri()
        {
            var logger = Mock.Of<ILogger>();
            Assert.Throws<UriFormatException>(() => Proxy.Parse("http://proxyserver:xyz", logger));
        }
    }
}
