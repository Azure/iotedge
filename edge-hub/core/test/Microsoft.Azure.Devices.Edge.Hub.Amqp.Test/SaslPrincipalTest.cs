// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class SaslPrincipalTest
    {
        [Fact]
        [Unit]
        public void TestNullConstructorInputs()
        {
            var edgeHubIdentity = Mock.Of<IClientCredentials>(i => i.Identity == Mock.Of<IIdentity>(id => id.Id == "dev1/mod1"));
            Assert.Throws<ArgumentNullException>(() => new SaslPrincipal(false, null));
            Assert.NotNull(new SaslPrincipal(true, edgeHubIdentity));
        }

        [Fact]
        [Unit]
        public void TestIsInRoleThrows()
        {
            var edgeHubIdentity = Mock.Of<IClientCredentials>(i => i.Identity == Mock.Of<IIdentity>(id => id.Id == "dev1/mod1"));
            var principal = new SaslPrincipal(true, edgeHubIdentity);
            Assert.Throws<NotImplementedException>(() => principal.IsInRole("boo"));
        }
    }
}
