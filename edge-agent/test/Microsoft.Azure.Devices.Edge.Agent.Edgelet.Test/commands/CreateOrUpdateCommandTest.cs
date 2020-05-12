// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Test.Commands
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Extensions.Configuration;
    using Moq;
    using Serilog.Events;
    using Xunit;

    [Unit]
    public class CreateOrUpdateCommandTest
    {
        [Fact]
        public void VerfiyEdgeAgentEnvironmentVariables()
        {
            var mockModuleManager = new Mock<IModuleManager>();
            var edgeAgentModule = new Mock<IEdgeAgentModule>();
            var edgeAgentModuleIdentity = new Mock<IModuleIdentity>();
            var configSource = new Mock<IConfigSource>();
            var configuration = new Mock<IConfiguration>();
            var settings = new object();

            edgeAgentModule.Setup(m => m.Name).Returns("edgeAgent");
            edgeAgentModule.Setup(m => m.Type).Returns("type1");
            edgeAgentModule.Setup(m => m.ImagePullPolicy).Returns(ImagePullPolicy.OnCreate);
            edgeAgentModule.Setup(m => m.Env).Returns(
                new Dictionary<string, EnvVal>
                {
                    { "EnvKey1", new EnvVal("EnvValue1") },
                    { "EnvKey2", new EnvVal("EnvValue2") },
                });

            edgeAgentModuleIdentity.Setup(id => id.DeviceId).Returns("MyEdgeDeviceId");
            edgeAgentModuleIdentity.Setup(id => id.ModuleId).Returns("$edgeAgent");
            edgeAgentModuleIdentity.Setup(id => id.Credentials).Returns(
                new IdentityProviderServiceCredentials("identityProviderUri1", "moduleGenerationId1", "authScheme1"));
            edgeAgentModuleIdentity.Setup(id => id.IotHubHostname).Returns("MyTestIoTHub");
            edgeAgentModuleIdentity.Setup(id => id.GatewayHostname).Returns("MyParentEdge");
            edgeAgentModuleIdentity.Setup(id => id.EdgeDeviceHostname).Returns("MyEdgeDevice");

            configSource.Setup(cs => cs.Configuration).Returns(configuration.Object);
            var upstreamProtocolConfig = new Mock<IConfigurationSection>();
            configuration.Setup(c => c.GetSection(Constants.UpstreamProtocolKey)).Returns(upstreamProtocolConfig.Object);
            upstreamProtocolConfig.Setup(c => c.Value).Returns("Amqp");
            var edgeletManagementUriConfig = new Mock<IConfigurationSection>();
            configuration.Setup(c => c.GetSection(Constants.EdgeletManagementUriVariableName)).Returns(edgeletManagementUriConfig.Object);
            edgeletManagementUriConfig.Setup(c => c.Value).Returns("Edgelet_Management_Uri");
            var networkIdConfig = new Mock<IConfigurationSection>();
            configuration.Setup(c => c.GetSection(Constants.NetworkIdKey)).Returns(networkIdConfig.Object);
            networkIdConfig.Setup(c => c.Value).Returns("iotedge-network");
            var edgeletApiVersionConfig = new Mock<IConfigurationSection>();
            configuration.Setup(c => c.GetSection(Constants.EdgeletApiVersionVariableName)).Returns(edgeletApiVersionConfig.Object);
            edgeletApiVersionConfig.Setup(c => c.Value).Returns("2020.01.01");

            CreateOrUpdateCommand command = CreateOrUpdateCommand.BuildCreate(
                mockModuleManager.Object, edgeAgentModule.Object, edgeAgentModuleIdentity.Object, configSource.Object, settings);

            Assert.Equal("edgeAgent", command.ModuleSpec.Name);
            Assert.Equal("type1", command.ModuleSpec.Type);
            Assert.Equal(ImagePullPolicy.OnCreate, command.ModuleSpec.ImagePullPolicy);
            Assert.Equal(settings, command.ModuleSpec.Settings);
            List<Models.EnvVar> environmentVariables = command.ModuleSpec.EnvironmentVariables.ToList();
            Assert.Equal("EnvValue1", environmentVariables.Where(v => v.Key.Equals("EnvKey1")).First().Value);
            Assert.Equal("EnvValue2", environmentVariables.Where(v => v.Key.Equals("EnvKey2")).First().Value);
            Assert.Equal("identityProviderUri1", environmentVariables.Where(v => v.Key.Equals(Constants.EdgeletWorkloadUriVariableName)).First().Value);
            Assert.Equal("authScheme1", environmentVariables.Where(v => v.Key.Equals(Constants.EdgeletAuthSchemeVariableName)).First().Value);
            Assert.Equal("moduleGenerationId1", environmentVariables.Where(v => v.Key.Equals(Constants.EdgeletModuleGenerationIdVariableName)).First().Value);
            Assert.Equal("MyTestIoTHub", environmentVariables.Where(v => v.Key.Equals(Constants.IotHubHostnameVariableName)).First().Value);
            Assert.Equal("MyParentEdge", environmentVariables.Where(v => v.Key.Equals(Constants.GatewayHostnameVariableName)).First().Value);
            Assert.Equal("MyEdgeDevice", environmentVariables.Where(v => v.Key.Equals(Constants.EdgeDeviceHostNameKey)).First().Value);
            Assert.Equal("MyEdgeDeviceId", environmentVariables.Where(v => v.Key.Equals(Constants.DeviceIdVariableName)).First().Value);
            Assert.Equal("$edgeAgent", environmentVariables.Where(v => v.Key.Equals(Constants.ModuleIdVariableName)).First().Value);
            Assert.Equal(LogEventLevel.Information.ToString(), environmentVariables.Where(v => v.Key.Equals(Logger.RuntimeLogLevelEnvKey)).First().Value);
            Assert.Equal("Amqp", environmentVariables.Where(v => v.Key.Equals(Constants.UpstreamProtocolKey)).First().Value);
            Assert.Equal("Edgelet_Management_Uri", environmentVariables.Where(v => v.Key.Equals(Constants.EdgeletManagementUriVariableName)).First().Value);
            Assert.Equal("iotedge-network", environmentVariables.Where(v => v.Key.Equals(Constants.NetworkIdKey)).First().Value);
            Assert.Equal("iotedged", environmentVariables.Where(v => v.Key.Equals(Constants.ModeKey)).First().Value);
            Assert.Equal("2020.01.01", environmentVariables.Where(v => v.Key.Equals(Constants.EdgeletApiVersionVariableName)).First().Value);
        }
    }
}
