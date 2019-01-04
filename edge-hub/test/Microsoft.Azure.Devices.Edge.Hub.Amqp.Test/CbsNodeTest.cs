// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class CbsNodeTest
    {
        const string IoTHubHostName = "someIotHub1.azure-devices.net";

        [Fact]
        public void ValidateTestSucceeds()
        {
            // Arrange
            string sasToken = TokenHelper.CreateSasToken(IoTHubHostName);
            var amqpValue = new AmqpValue
            {
                Value = sasToken
            };
            AmqpMessage validAmqpMessage = AmqpMessage.Create(amqpValue);
            validAmqpMessage.ApplicationProperties.Map[CbsConstants.PutToken.Type] = "azure-devices.net:sastoken";
            validAmqpMessage.ApplicationProperties.Map[CbsConstants.PutToken.Audience] = "iothub";
            validAmqpMessage.ApplicationProperties.Map[CbsConstants.Operation] = CbsConstants.PutToken.OperationValue;

            // Act
            (string token, string audience) = CbsNode.ValidateAndParseMessage(IoTHubHostName, validAmqpMessage);

            // Assert
            Assert.Equal(sasToken, token);
            Assert.Equal(IoTHubHostName, audience);
        }

        [Fact]
        public void ValidateTestThrowsOnInvalidTokenType()
        {
            // Arrange
            var amqpValue = new AmqpValue
            {
                Value = TokenHelper.CreateSasToken(IoTHubHostName)
            };
            AmqpMessage invalidAmqpMessage1 = AmqpMessage.Create(amqpValue);
            invalidAmqpMessage1.ApplicationProperties.Map[CbsConstants.PutToken.Type] = "foobar";
            invalidAmqpMessage1.ApplicationProperties.Map[CbsConstants.PutToken.Audience] = "iothub";
            invalidAmqpMessage1.ApplicationProperties.Map[CbsConstants.Operation] = CbsConstants.PutToken.OperationValue;
            // Act/Assert
            Assert.Throws<InvalidOperationException>(() => CbsNode.ValidateAndParseMessage(IoTHubHostName, invalidAmqpMessage1));
        }

        [Fact]
        public void ValidateTestThrowsOnInvalidAudience()
        {
            // Arrange
            var amqpValue = new AmqpValue
            {
                Value = TokenHelper.CreateSasToken(IoTHubHostName)
            };
            AmqpMessage invalidAmqpMessage2 = AmqpMessage.Create(amqpValue);
            invalidAmqpMessage2.ApplicationProperties.Map[CbsConstants.PutToken.Type] = "azure-devices.net:sastoken";
            invalidAmqpMessage2.ApplicationProperties.Map[CbsConstants.PutToken.Audience] = "";
            invalidAmqpMessage2.ApplicationProperties.Map[CbsConstants.Operation] = CbsConstants.PutToken.OperationValue;
            // Act/Assert
            Assert.Throws<InvalidOperationException>(() => CbsNode.ValidateAndParseMessage(IoTHubHostName, invalidAmqpMessage2));
        }

        [Fact]
        public void ValidateTestThrowsOnInvalidOperation()
        {
            // Arrange
            var amqpValue = new AmqpValue
            {
                Value = TokenHelper.CreateSasToken(IoTHubHostName)
            };
            AmqpMessage invalidAmqpMessage3 = AmqpMessage.Create(amqpValue);
            invalidAmqpMessage3.ApplicationProperties.Map[CbsConstants.PutToken.Type] = "azure-devices.net:sastoken";
            invalidAmqpMessage3.ApplicationProperties.Map[CbsConstants.PutToken.Audience] = "iothub";
            invalidAmqpMessage3.ApplicationProperties.Map[CbsConstants.Operation] = "foobar";
            // Act/Assert
            Assert.Throws<InvalidOperationException>(() => CbsNode.ValidateAndParseMessage(IoTHubHostName, invalidAmqpMessage3));
        }

        [Fact]
        public void ValidateTestThrowsOnInvalidContents()
        {
            // Arrange
            AmqpMessage invalidAmqpMessage4 = AmqpMessage.Create(new AmqpValue { Value = "azure-devices.net:sastoken" });
            invalidAmqpMessage4.ApplicationProperties.Map[CbsConstants.PutToken.Type] = "azure-devices.net:sastoken";
            invalidAmqpMessage4.ApplicationProperties.Map[CbsConstants.PutToken.Audience] = "iothub";
            invalidAmqpMessage4.ApplicationProperties.Map[CbsConstants.Operation] = CbsConstants.PutToken.OperationValue;
            // Act/Assert
            Assert.Throws<InvalidOperationException>(() => CbsNode.ValidateAndParseMessage(IoTHubHostName, invalidAmqpMessage4));
        }

        [Fact]
        public void ParseIdsTest()
        {
            string deviceId;
            string moduleId;

            // Arrange
            string audience = "edgehubtest1.azure-devices.net/devices/device1";
            // Act
            (deviceId, moduleId) = CbsNode.ParseIds(audience);
            // Assert
            Assert.Equal("device1", deviceId);
            Assert.Null(moduleId);

            // Arrange
            audience = "edgehubtest1.azure-devices.net/devices/device1/modules/mod1";
            // Act
            (deviceId, moduleId) = CbsNode.ParseIds(audience);
            // Assert
            Assert.Equal("device1", deviceId);
            Assert.Equal("mod1", moduleId);

            // Arrange
            audience = "edgehubtest1.azure-devices.net/device/device1/module/mod1";
            // Act/Assert
            Assert.Throws<InvalidOperationException>(() => CbsNode.ParseIds(audience));

            // Arrange
            audience = "edgehubtest1.azure-devices.net/devices/d%40n.f/modules/m%40n.p";
            // Act
            (deviceId, moduleId) = CbsNode.ParseIds(audience);
            // Assert
            Assert.Equal("d@n.f", deviceId);
            Assert.Equal("m@n.p", moduleId);
        }

        [Fact]
        public void GetIdentityTest()
        {
            // Arrange
            var amqpValue = new AmqpValue
            {
                Value = TokenHelper.CreateSasToken("edgehubtest1.azure-devices.net/devices/device1/modules/mod1")
            };
            AmqpMessage validAmqpMessage = AmqpMessage.Create(amqpValue);
            validAmqpMessage.ApplicationProperties.Map[CbsConstants.PutToken.Type] = "azure-devices.net:sastoken";
            validAmqpMessage.ApplicationProperties.Map[CbsConstants.PutToken.Audience] = "iothub";
            validAmqpMessage.ApplicationProperties.Map[CbsConstants.Operation] = CbsConstants.PutToken.OperationValue;

            var clientCredentials = Mock.Of<IClientCredentials>();
            var identityFactory = new Mock<IClientCredentialsFactory>();
            identityFactory.Setup(i => i.GetWithSasToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
                .Returns(clientCredentials);

            string iotHubHostName = "edgehubtest1.azure-devices.net";
            var authenticator = new Mock<IAuthenticator>();
            var cbsNode = new CbsNode(identityFactory.Object, iotHubHostName, authenticator.Object, new NullCredentialsCache());

            // Act
            IClientCredentials receivedClientCredentials = cbsNode.GetClientCredentials(validAmqpMessage);

            // Assert
            Assert.NotNull(receivedClientCredentials);
            Assert.Equal(clientCredentials, receivedClientCredentials);
        }

        [Fact]
        public async Task UpdateCbsTokenTest()
        {
            // Arrange
            var amqpValue = new AmqpValue
            {
                Value = TokenHelper.CreateSasToken("edgehubtest1.azure-devices.net/devices/device1/modules/mod1")
            };
            AmqpMessage validAmqpMessage = AmqpMessage.Create(amqpValue);
            validAmqpMessage.ApplicationProperties.Map[CbsConstants.PutToken.Type] = "azure-devices.net:sastoken";
            validAmqpMessage.ApplicationProperties.Map[CbsConstants.PutToken.Audience] = "iothub";
            validAmqpMessage.ApplicationProperties.Map[CbsConstants.Operation] = CbsConstants.PutToken.OperationValue;

            var identity = Mock.Of<IIdentity>(i => i.Id == "device1/mod1");
            var clientCredentials = Mock.Of<IClientCredentials>(c => c.Identity == identity);
            var clientCredentialsFactory = new Mock<IClientCredentialsFactory>();
            clientCredentialsFactory.Setup(i => i.GetWithSasToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
                .Returns(clientCredentials);

            string iotHubHostName = "edgehubtest1.azure-devices.net";
            var authenticator = new Mock<IAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<IClientCredentials>())).ReturnsAsync(true);
            var cbsNode = new CbsNode(clientCredentialsFactory.Object, iotHubHostName, authenticator.Object, new NullCredentialsCache());

            // Act
            (AmqpResponseStatusCode statusCode, string description) = await cbsNode.UpdateCbsToken(validAmqpMessage);
            bool isAuthenticated = await cbsNode.AuthenticateAsync(identity.Id);

            // Assert
            Assert.True(isAuthenticated);
            Assert.Equal(AmqpResponseStatusCode.OK, statusCode);
            Assert.Equal(AmqpResponseStatusCode.OK.ToString(), description);
        }
    }
}
