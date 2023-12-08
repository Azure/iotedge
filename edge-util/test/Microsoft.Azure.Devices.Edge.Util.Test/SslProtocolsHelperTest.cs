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
        [InlineData("Tls12, Tls13", SslProtocols.None, SslProtocols.Tls12 | SslProtocols.Tls13)]
        [InlineData("Tls1.2, foo, Tls1.3", SslProtocols.Tls12, SslProtocols.Tls12 | SslProtocols.Tls13)]
        [InlineData("Tls1.2, Tls1.3", SslProtocols.None, SslProtocols.Tls12 | SslProtocols.Tls13)]
        [InlineData("tls12, tls13", SslProtocols.None, SslProtocols.Tls12 | SslProtocols.Tls13)]
        [InlineData("Tls12, Tls13", SslProtocols.None, SslProtocols.Tls12 | SslProtocols.Tls13)]
        [InlineData("tls1_2, tls1_3", SslProtocols.None, SslProtocols.Tls12 | SslProtocols.Tls13)]
        [InlineData("Tls1_2, Tls1_3", SslProtocols.None, SslProtocols.Tls12 | SslProtocols.Tls13)]
        [InlineData("tlsv12, tlsv13", SslProtocols.None, SslProtocols.Tls12 | SslProtocols.Tls13)]
        [InlineData("Tlsv12, Tlsv13", SslProtocols.None, SslProtocols.Tls12 | SslProtocols.Tls13)]
        [InlineData("TLS12", SslProtocols.None, SslProtocols.Tls12)]
        public void ParseTest(string input, SslProtocols defaultSslProtocols, SslProtocols expectedSslProtocols)
        {
            SslProtocols sslProtocols = SslProtocolsHelper.Parse(input, defaultSslProtocols, Mock.Of<ILogger>());
            Assert.Equal(expectedSslProtocols, sslProtocols);
        }

        [Theory]
        [InlineData(SslProtocols.Tls12, "Tls12")]
        [InlineData(SslProtocols.Tls13, "Tls13")]
        [InlineData(SslProtocols.Tls12 | SslProtocols.Tls13, "Tls12, Tls13")]
        public void PrintTest(SslProtocols sslProtocols, string expectedString)
        {
            Assert.Equal(expectedString, sslProtocols.Print());
        }
    }
}
