// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Edged;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    using ObjectUnderTestFn = System.Func<
        Microsoft.Azure.Devices.Edge.Util.ITokenProvider,
        Microsoft.Azure.Devices.Client.AuthenticationWithTokenRefresh>;

    [Unit]
    public class AuthenticationWithTokenRefreshTest
    {
        public static IEnumerable<object[]> GetObjectUnderTest()
        {
            yield return new ObjectUnderTestFn[]
            {
                p => new DeviceAuthentication(p, "d")
            };
            yield return new ObjectUnderTestFn[]
            {
                p => new ModuleAuthentication(p, "d", "m")
            };
        }

        [Theory]
        [MemberData(nameof(GetObjectUnderTest))]
        public async Task SafeCreateNewTokenReturnsAToken(ObjectUnderTestFn get)
        {
            var auth = get(new TestTokenProvider());
            Assert.NotEmpty(await auth.GetTokenAsync("iothub"));
        }

        [Theory]
        [MemberData(nameof(GetObjectUnderTest))]
        public async Task SafeCreateNewTokenWrapsTokenExceptions(ObjectUnderTestFn get)
        {
            var src = new TokenProviderException(new Exception("hello"));
            var auth = get(new ThrowingTestTokenProvider(src));

            var dst = await Assert.ThrowsAnyAsync<IotHubCommunicationException>(
                () => auth.GetTokenAsync("iothub"));
            Assert.Equal(src.Message, dst.Message);
        }
    }

    class TestTokenProvider : ITokenProvider
    {
        public Task<string> GetTokenAsync(Option<TimeSpan> ttl)
        {
            var builder = new SharedAccessSignatureBuilder
            {
                Key = Convert.ToBase64String(Encoding.UTF8.GetBytes("hello")),
                TimeToLive = ttl.GetOrElse(TimeSpan.FromHours(1))
            };

            return Task.FromResult(builder.ToSignature());
        }
    }

    class ThrowingTestTokenProvider : ITokenProvider
    {
        TokenProviderException e;

        public ThrowingTestTokenProvider(TokenProviderException e)
        {
            this.e = e;
        }

        public Task<string> GetTokenAsync(Option<TimeSpan> ttl)
        {
            throw this.e;
        }
    }
}