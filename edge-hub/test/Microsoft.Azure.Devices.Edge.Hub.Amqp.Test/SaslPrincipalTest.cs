namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using System;
    using Xunit;

    public class SaslPrincipalTest
    {
        [Fact]
        [Unit]
        public void TestNullConstructorInputs()
        {
            SaslIdentity saslIdentity = SaslIdentity.Parse("dev1/modules/mod1@sas.hub1");
            var edgeHubIdentity = new Mock<IIdentity>();

            Assert.Throws<ArgumentNullException>(() => new SaslPrincipal(null, edgeHubIdentity.Object));
            Assert.Throws<ArgumentNullException>(() => new SaslPrincipal(saslIdentity, null));
            Assert.NotNull(new SaslPrincipal(saslIdentity, edgeHubIdentity.Object));
        }

        [Fact]
        [Unit]
        public void TestIsInRoleThrows()
        {
            SaslIdentity saslIdentity = SaslIdentity.Parse("dev1/modules/mod1@sas.hub1");
            var edgeHubIdentity = new Mock<IIdentity>();
            var principal = new SaslPrincipal(saslIdentity, edgeHubIdentity.Object);

            Assert.Throws<InvalidOperationException>(() => principal.IsInRole("boo"));
        }
    }
}
