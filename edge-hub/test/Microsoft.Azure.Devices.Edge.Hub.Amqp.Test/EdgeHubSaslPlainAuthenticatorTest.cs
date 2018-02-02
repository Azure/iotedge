// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using IIdentity = Microsoft.Azure.Devices.Edge.Hub.Core.Identity.IIdentity;

    public class EdgeHubSaslPlainAuthenticatorTest
    {
        [Fact]
        [Unit]
        public void TestNullConstructorInputs()
        {
            var authenticator = new Mock<IAuthenticator>();
            var identityFactory = new Mock<IIdentityFactory>();

            Assert.Throws<ArgumentNullException>(() => new EdgeHubSaslPlainAuthenticator(null, identityFactory.Object));
            Assert.Throws<ArgumentNullException>(() => new EdgeHubSaslPlainAuthenticator(authenticator.Object, null));
            Assert.NotNull(new EdgeHubSaslPlainAuthenticator(authenticator.Object, identityFactory.Object));
        }

        [Fact]
        [Unit]
        public async void TestBadInputsToAuthenticateAsync()
        {
            var authenticator = new Mock<IAuthenticator>();
            var identityFactory = new Mock<IIdentityFactory>();
            var saslAuthenticator = new EdgeHubSaslPlainAuthenticator(authenticator.Object, identityFactory.Object);

            await Assert.ThrowsAsync<ArgumentException>(() => saslAuthenticator.AuthenticateAsync(null, "pwd"));
            await Assert.ThrowsAsync<ArgumentException>(() => saslAuthenticator.AuthenticateAsync("uid", null));
        }

        [Fact]
        [Unit]
        public async void TestNoDeviceId()
        {
            var authenticator = new Mock<IAuthenticator>();
            var identityFactory = new Mock<IIdentityFactory>();
            var saslAuthenticator = new EdgeHubSaslPlainAuthenticator(authenticator.Object, identityFactory.Object);
            const string UserId = "key1@sas.root.hub1";

            await Assert.ThrowsAsync<EdgeHubConnectionException>(() => saslAuthenticator.AuthenticateAsync(UserId, "pwd"));
        }

        [Fact]
        [Unit]
        public async void TestGetSasTokenFailed()
        {
            var authenticator = new Mock<IAuthenticator>();
            var identityFactory = new Mock<IIdentityFactory>();
            var saslAuthenticator = new EdgeHubSaslPlainAuthenticator(authenticator.Object, identityFactory.Object);
            const string UserId = "dev1/modules/mod1@sas.hub1";
            const string Password = "pwd";

            identityFactory.Setup(f => f.GetWithSasToken("dev1", "mod1", string.Empty, true, Password))
                .Returns(Try<IIdentity>.Failure(new ApplicationException("Bad donut")));

            await Assert.ThrowsAsync<EdgeHubConnectionException>(() => saslAuthenticator.AuthenticateAsync(UserId, Password));
        }

        [Fact]
        [Unit]
        public async void TestAuthFailed()
        {
            var authenticator = new Mock<IAuthenticator>();
            var identityFactory = new Mock<IIdentityFactory>();
            var edgeHubIdentity = new Mock<IIdentity>();
            var saslAuthenticator = new EdgeHubSaslPlainAuthenticator(authenticator.Object, identityFactory.Object);
            const string UserId = "dev1/modules/mod1@sas.hub1";
            const string Password = "pwd";

            identityFactory.Setup(f => f.GetWithSasToken("dev1", "mod1", string.Empty, true, Password))
                .Returns(Try.Success(edgeHubIdentity.Object));
            authenticator.Setup(a => a.AuthenticateAsync(edgeHubIdentity.Object))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<EdgeHubConnectionException>(() => saslAuthenticator.AuthenticateAsync(UserId, Password));
        }

        [Fact]
        [Unit]
        public async void TestAuthSucceeds()
        {
            var authenticator = new Mock<IAuthenticator>();
            var identityFactory = new Mock<IIdentityFactory>();
            var edgeHubIdentity = new Mock<IIdentity>();
            var saslAuthenticator = new EdgeHubSaslPlainAuthenticator(authenticator.Object, identityFactory.Object);
            const string UserId = "dev1/modules/mod1@sas.hub1";
            const string Password = "pwd";

            identityFactory.Setup(f => f.GetWithSasToken("dev1", "mod1", string.Empty, true, Password))
                .Returns(Try.Success(edgeHubIdentity.Object));
            authenticator.Setup(a => a.AuthenticateAsync(edgeHubIdentity.Object))
                .ReturnsAsync(true);
            edgeHubIdentity.SetupGet(ehid => ehid.Id).Returns("dev1");

            var principal = await saslAuthenticator.AuthenticateAsync(UserId, Password) as SaslPrincipal;
            Assert.NotNull(principal);
            Assert.NotNull(principal.Identity);
            Assert.Equal(edgeHubIdentity.Object, principal.EdgeHubIdentity);
        }
    }
}
