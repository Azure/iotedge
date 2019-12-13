// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System.Security.Authentication;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    [Unit]
    public class SslProtocolsHelperTest
    {
        [Theory]
        [InlineData(null, SslProtocols.Tls12, SslProtocols.Tls12)]
        [InlineData("", SslProtocols.Tls12, SslProtocols.Tls12)]
        [InlineData("foo", SslProtocols.Tls12, SslProtocols.Tls12)]
        [InlineData("Tls1.2", SslProtocols.None, SslProtocols.Tls12)]
        [InlineData("Tls12", SslProtocols.None, SslProtocols.Tls12)]
        [InlineData("Tls12, Tls1.2", SslProtocols.None, SslProtocols.Tls12)]
        [InlineData("Tls11, Tls12", SslProtocols.None, SslProtocols.Tls11 | SslProtocols.Tls12)]
        [InlineData("Tls1.1, Tls1.2, foo, Tls", SslProtocols.Tls12, SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls)]
        [InlineData("Tls, Tls1.1, Tls1.2", SslProtocols.None, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12)]
        [InlineData("tls, tls11, tls12", SslProtocols.None, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12)]
        [InlineData("Tls, Tls11, Tls12", SslProtocols.None, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12)]
        [InlineData("TLS12", SslProtocols.None, SslProtocols.Tls12)]
        public void ParseTest(string input, SslProtocols defaultSslProtocols, SslProtocols expectedSslProtocols)
        {
            SslProtocols sslProtocols = SslProtocolsHelper.Parse(input, defaultSslProtocols, Mock.Of<ILogger>());
            Assert.Equal(expectedSslProtocols, sslProtocols);
        }

        [Theory]
        [InlineData(SslProtocols.Tls, "Tls")]
        [InlineData(SslProtocols.Tls11, "Tls11")]
        [InlineData(SslProtocols.Tls12, "Tls12")]
        [InlineData(SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, "Tls, Tls11, Tls12")]
        public void PrintTest(SslProtocols sslProtocols, string expectedString)
        {
            Assert.Equal(expectedString, sslProtocols.Print());
        }
    }
}
