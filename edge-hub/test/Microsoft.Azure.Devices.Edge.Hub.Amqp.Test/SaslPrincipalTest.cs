namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Xunit;

    public class SaslPrincipalTest
    {
        [Fact]
        [Unit]
        public void TestNullConstructorInputs()
        {
            SaslIdentity saslIdentity = SaslIdentity.Parse("dev1/modules/mod1@sas.hub1");
            var edgeHubIdentity = Mock.Of<IIdentity>(i => i.Id == "dev1/mod1");

            Assert.Throws<ArgumentNullException>(() => new SaslPrincipal(null, new AmqpAuthentication(true, Option.Some(edgeHubIdentity))));
            Assert.Throws<ArgumentNullException>(() => new SaslPrincipal(saslIdentity, null));
            Assert.NotNull(new SaslPrincipal(saslIdentity, new AmqpAuthentication(true, Option.Some(edgeHubIdentity))));
        }

        [Fact]
        [Unit]
        public void TestIsInRoleThrows()
        {
            SaslIdentity saslIdentity = SaslIdentity.Parse("dev1/modules/mod1@sas.hub1");
            var edgeHubIdentity = Mock.Of<IIdentity>(i => i.Id == "dev1/mod1");
            var principal = new SaslPrincipal(saslIdentity, new AmqpAuthentication(true, Option.Some(edgeHubIdentity)));

            Assert.Throws<NotImplementedException>(() => principal.IsInRole("boo"));
        }
    }
}
