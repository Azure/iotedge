// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;

    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    using Moq;

    using Xunit;

    public class SaslPrincipalTest
    {
        [Fact]
        [Unit]
        public void TestIsInRoleThrows()
        {
            var edgeHubIdentity = Mock.Of<IClientCredentials>(i => i.Identity == Mock.Of<IIdentity>(id => id.Id == "dev1/mod1"));
            var principal = new SaslPrincipal(new AmqpAuthentication(true, Option.Some(edgeHubIdentity)));
            Assert.Throws<NotImplementedException>(() => principal.IsInRole("boo"));
        }

        [Fact]
        [Unit]
        public void TestNullConstructorInputs()
        {
            var edgeHubIdentity = Mock.Of<IClientCredentials>(i => i.Identity == Mock.Of<IIdentity>(id => id.Id == "dev1/mod1"));
            Assert.Throws<ArgumentNullException>(() => new SaslPrincipal(null));
            Assert.NotNull(new SaslPrincipal(new AmqpAuthentication(true, Option.Some(edgeHubIdentity))));
        }
    }
}
