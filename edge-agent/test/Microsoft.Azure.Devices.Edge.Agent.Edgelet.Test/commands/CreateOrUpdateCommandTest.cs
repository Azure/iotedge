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
        [Theory]
        [MemberData(nameof(TestDataCollection))]
        public void VerifyCreateOrUpdateCommand(CreateOrUpdateCommandTestData testData)
        {
            var mocks = new CreateOrUpdateCommandMocks(testData);

            CreateOrUpdateCommand command = CreateOrUpdateCommand.BuildCreate(
                mocks.ModuleManager.Object,
                mocks.EdgeAgentModule.Object,
                mocks.EdgeAgentModuleIdentity.Object,
                mocks.ConfigSource.Object,
                testData.Settings,
                testData.EdgeDeviceHostname,
                testData.ParentEdgeHostname);

            this.VerifyResult(testData, command);
        }

        public static IEnumerable<object[]> TestDataCollection =>
            new List<object[]>
            {
                new object[]
                {
                    new CreateOrUpdateCommandTestData(
                        "edgeAgent",
                        "type1",
                        ImagePullPolicy.OnCreate,
                        new Dictionary<string, EnvVal>
                        {
                            { "EnvKey1", new EnvVal("EnvValue1") },
                            { "EnvKey2", new EnvVal("EnvValue2") },
                        },
                        "MyEdgeDeviceId",
                        "$edgeAgent",
                        "workloadUri1",
                        "moduleGenerationId1",
                        "authScheme1",
                        "MyTestIoTHub",
                        "MyEdgeDevice",
                        Option.None<string>(),
                        "Amqp",
                        "Edgelet_Management_Uri",
                        "Edgelet_Listen_workload_Uri",
                        "iotedge-network",
                        "2020.01.01",
                        new object())
                },
                new object[]
                {
                    new CreateOrUpdateCommandTestData(
                        "edgeAgent",
                        "type1",
                        ImagePullPolicy.OnCreate,
                        new Dictionary<string, EnvVal>
                        {
                            { "EnvKey1", new EnvVal("EnvValue1") },
                            { "EnvKey2", new EnvVal("EnvValue2") },
                        },
                        "MyEdgeDeviceId",
                        "$edgeAgent",
                        "workloadUri1",
                        "moduleGenerationId1",
                        "authScheme1",
                        "MyTestIoTHub",
                        "MyEdgeDevice",
                        Option.Some("parentEdgeHost999"),
                        "Amqp",
                        "Edgelet_Management_Uri",
                        "Edgelet_Listen_workload_Uri",
                        "iotedge-network",
                        "2020.01.01",
                        new object())
                },
                new object[]
                {
                    new CreateOrUpdateCommandTestData(
                        "edgeHub",
                        "type1",
                        ImagePullPolicy.OnCreate,
                        new Dictionary<string, EnvVal>
                        {
                            { "EnvKey1", new EnvVal("EnvValue1") },
                            { "EnvKey2", new EnvVal("EnvValue2") },
                        },
                        "MyEdgeDeviceId",
                        "$edgeHub",
                        "workloadUri1",
                        "moduleGenerationId1",
                        "authScheme1",
                        "MyTestIoTHub",
                        "MyEdgeDevice",
                        Option.None<string>(),
                        "Amqp",
                        "Edgelet_Management_Uri",
                        "Edgelet_Listen_workload_Uri",
                        "iotedge-network",
                        "2020.01.01",
                        new object())
                },
                new object[]
                {
                    new CreateOrUpdateCommandTestData(
                        "edgeHub",
                        "type1",
                        ImagePullPolicy.OnCreate,
                        new Dictionary<string, EnvVal>
                        {
                            { "EnvKey1", new EnvVal("EnvValue1") },
                            { "EnvKey2", new EnvVal("EnvValue2") },
                        },
                        "MyEdgeDeviceId",
                        "$edgeHub",
                        "workloadUri1",
                        "moduleGenerationId1",
                        "authScheme1",
                        "MyTestIoTHub",
                        "MyEdgeDevice",
                        Option.Some("parentEdgeHost999"),
                        "Amqp",
                        "Edgelet_Management_Uri",
                        "Edgelet_Listen_workload_Uri",
                        "iotedge-network",
                        "2020.01.01",
                        new object())
                },
                new object[]
                {
                    new CreateOrUpdateCommandTestData(
                        "tempSensor",
                        "type1",
                        ImagePullPolicy.OnCreate,
                        new Dictionary<string, EnvVal>
                        {
                            { "EnvKey1", new EnvVal("EnvValue1") },
                            { "EnvKey2", new EnvVal("EnvValue2") },
                        },
                        "MyEdgeDeviceId",
                        "tempSensor",
                        "workloadUri1",
                        "moduleGenerationId1",
                        "authScheme1",
                        "MyTestIoTHub",
                        "MyEdgeDevice",
                        Option.None<string>(),
                        "Amqp",
                        "Edgelet_Management_Uri",
                        "Edgelet_Listen_workload_Uri",
                        "iotedge-network",
                        "2020.01.01",
                        new object())
                },
                new object[]
                {
                    new CreateOrUpdateCommandTestData(
                        "tempSensor",
                        "type1",
                        ImagePullPolicy.Never,
                        new Dictionary<string, EnvVal>
                        {
                            { "EnvKey1", new EnvVal("EnvValue1") },
                            { "EnvKey2", new EnvVal("EnvValue2") },
                        },
                        "MyEdgeDeviceId",
                        "tempSensor",
                        "workloadUri1",
                        "moduleGenerationId1",
                        "authScheme1",
                        "MyTestIoTHub",
                        "MyEdgeDevice",
                        Option.Some("parentEdgeHost999"),
                        "Amqp",
                        "Edgelet_Management_Uri",
                        "Edgelet_Listen_workload_Uri",
                        "iotedge-network",
                        "2020.01.01",
                        new object())
                },
            };

        void VerifyResult(CreateOrUpdateCommandTestData testData, CreateOrUpdateCommand command)
        {
            Assert.Equal(testData.ModuleName, command.ModuleSpec.Name);
            Assert.Equal(testData.ModuleType, command.ModuleSpec.Type);
            Assert.Equal(testData.ImagePullPolicy, command.ModuleSpec.ImagePullPolicy);
            Assert.Equal(testData.Settings, command.ModuleSpec.Settings);

            List<Models.EnvVar> environmentVariables = command.ModuleSpec.EnvironmentVariables.ToList();

            foreach (string key in testData.EnvironmentVariables.Keys)
            {
                Assert.Equal(testData.EnvironmentVariables[key].Value, environmentVariables.First(v => v.Key.Equals(key)).Value);
            }

            Assert.Equal(testData.EdgeletWorkloadUri, environmentVariables.First(v => v.Key.Equals(Constants.EdgeletWorkloadUriVariableName)).Value);
            Assert.Equal(testData.EdgeletAuthScheme, environmentVariables.First(v => v.Key.Equals(Constants.EdgeletAuthSchemeVariableName)).Value);
            Assert.Equal(testData.ModuleGenerationId, environmentVariables.First(v => v.Key.Equals(Constants.EdgeletModuleGenerationIdVariableName)).Value);
            Assert.Equal(testData.IoTHubHostname, environmentVariables.First(v => v.Key.Equals(Constants.IotHubHostnameVariableName)).Value);
            Assert.Equal(testData.DeviceId, environmentVariables.First(v => v.Key.Equals(Constants.DeviceIdVariableName)).Value);
            Assert.Equal(testData.ModuleId, environmentVariables.First(v => v.Key.Equals(Constants.ModuleIdVariableName)).Value);
            Assert.Equal(LogEventLevel.Information.ToString(), environmentVariables.First(v => v.Key.Equals(Logger.RuntimeLogLevelEnvKey)).Value);
            Assert.Equal(testData.UpstreamProtocol, environmentVariables.First(v => v.Key.Equals(Constants.UpstreamProtocolKey)).Value);
            Assert.Equal(testData.EdgeletApiVersion, environmentVariables.First(v => v.Key.Equals(Constants.EdgeletApiVersionVariableName)).Value);

            if (testData.ModuleId.Equals(Constants.EdgeAgentModuleIdentityName))
            {
                Assert.Equal("iotedged", environmentVariables.First(v => v.Key.Equals(Constants.ModeKey)).Value);
                Assert.Equal(testData.EdgeletManagementUri, environmentVariables.First(v => v.Key.Equals(Constants.EdgeletManagementUriVariableName)).Value);
                Assert.Equal(testData.NetworkId, environmentVariables.First(v => v.Key.Equals(Constants.NetworkIdKey)).Value);
                Assert.Equal(testData.EdgeDeviceHostname, environmentVariables.First(v => v.Key.Equals(Constants.EdgeDeviceHostNameKey)).Value);
                testData.ParentEdgeHostname.ForEach(value =>
                    Assert.Equal(value, environmentVariables.First(v => v.Key.Equals(Constants.GatewayHostnameVariableName)).Value));
                Assert.Null(environmentVariables.FirstOrDefault(v => v.Key.Equals(Constants.ParentEdgeHostnameVariableName)));
            }
            else if (testData.ModuleId.Equals(Constants.EdgeHubModuleIdentityName))
            {
                Assert.Null(environmentVariables.FirstOrDefault(v => v.Key.Equals(Constants.ModeKey)));
                Assert.Null(environmentVariables.FirstOrDefault(v => v.Key.Equals(Constants.EdgeletManagementUriVariableName)));
                Assert.Null(environmentVariables.FirstOrDefault(v => v.Key.Equals(Constants.NetworkIdKey)));
                Assert.Equal(testData.EdgeDeviceHostname, environmentVariables.First(v => v.Key.Equals(Constants.EdgeDeviceHostNameKey)).Value);
                testData.ParentEdgeHostname.ForEach(value =>
                    Assert.Equal(value, environmentVariables.First(v => v.Key.Equals(Constants.GatewayHostnameVariableName)).Value));
                Assert.Null(environmentVariables.FirstOrDefault(v => v.Key.Equals(Constants.ParentEdgeHostnameVariableName)));
            }
            else
            {
                Assert.Null(environmentVariables.FirstOrDefault(v => v.Key.Equals(Constants.ModeKey)));
                Assert.Null(environmentVariables.FirstOrDefault(v => v.Key.Equals(Constants.EdgeletManagementUriVariableName)));
                Assert.Null(environmentVariables.FirstOrDefault(v => v.Key.Equals(Constants.NetworkIdKey)));
                Assert.Equal(testData.EdgeDeviceHostname, environmentVariables.First(v => v.Key.Equals(Constants.GatewayHostnameVariableName)).Value);
                testData.ParentEdgeHostname.ForEach(value =>
                    Assert.Equal(value, environmentVariables.First(v => v.Key.Equals(Constants.ParentEdgeHostnameVariableName)).Value));
                Assert.Null(environmentVariables.FirstOrDefault(v => v.Key.Equals(Constants.EdgeDeviceHostNameKey)));
            }
        }

        struct CreateOrUpdateCommandMocks
        {
            internal CreateOrUpdateCommandMocks(CreateOrUpdateCommandTestData testData)
            {
                this.ModuleManager = new Mock<IModuleManager>();
                this.EdgeAgentModule = new Mock<IEdgeAgentModule>();
                this.EdgeAgentModuleIdentity = new Mock<IModuleIdentity>();
                this.ConfigSource = new Mock<IConfigSource>();
                this.Configuration = new Mock<IConfiguration>();

                this.EdgeAgentModule.Setup(m => m.Name).Returns(testData.ModuleName);
                this.EdgeAgentModule.Setup(m => m.Type).Returns(testData.ModuleType);
                this.EdgeAgentModule.Setup(m => m.ImagePullPolicy).Returns(testData.ImagePullPolicy);
                this.EdgeAgentModule.Setup(m => m.Env).Returns(testData.EnvironmentVariables);

                this.EdgeAgentModuleIdentity.Setup(id => id.DeviceId).Returns(testData.DeviceId);
                this.EdgeAgentModuleIdentity.Setup(id => id.ModuleId).Returns(testData.ModuleId);
                this.EdgeAgentModuleIdentity.Setup(id => id.Credentials).Returns(testData.ModuleCredentials);
                this.EdgeAgentModuleIdentity.Setup(id => id.IotHubHostname).Returns(testData.IoTHubHostname);

                this.ConfigSource.Setup(cs => cs.Configuration).Returns(this.Configuration.Object);
                var upstreamProtocolConfig = new Mock<IConfigurationSection>();
                this.Configuration.Setup(c => c.GetSection(Constants.UpstreamProtocolKey)).Returns(upstreamProtocolConfig.Object);
                upstreamProtocolConfig.Setup(c => c.Value).Returns(testData.UpstreamProtocol);
                var edgeletManagementUriConfig = new Mock<IConfigurationSection>();
                this.Configuration.Setup(c => c.GetSection(Constants.EdgeletManagementUriVariableName)).Returns(edgeletManagementUriConfig.Object);
                edgeletManagementUriConfig.Setup(c => c.Value).Returns(testData.EdgeletManagementUri);
                var edgeletWorkloadListenUriMntConfig = new Mock<IConfigurationSection>();
                this.Configuration.Setup(c => c.GetSection(Constants.EdgeletWorkloadListenMntUriVariableName)).Returns(edgeletWorkloadListenUriMntConfig.Object);
                edgeletWorkloadListenUriMntConfig.Setup(c => c.Value).Returns(testData.EdgeletWorkloadListenUriMnt);
                var networkIdConfig = new Mock<IConfigurationSection>();
                this.Configuration.Setup(c => c.GetSection(Constants.NetworkIdKey)).Returns(networkIdConfig.Object);
                networkIdConfig.Setup(c => c.Value).Returns(testData.NetworkId);
                var edgeletApiVersionConfig = new Mock<IConfigurationSection>();
                this.Configuration.Setup(c => c.GetSection(Constants.EdgeletApiVersionVariableName)).Returns(edgeletApiVersionConfig.Object);
                edgeletApiVersionConfig.Setup(c => c.Value).Returns(testData.EdgeletApiVersion);
            }

            internal Mock<IModuleManager> ModuleManager { get; }

            internal Mock<IEdgeAgentModule> EdgeAgentModule { get; }

            internal Mock<IModuleIdentity> EdgeAgentModuleIdentity { get; }

            internal Mock<IConfigSource> ConfigSource { get; }

            internal Mock<IConfiguration> Configuration { get; }
        }

        public struct CreateOrUpdateCommandTestData
        {
            internal CreateOrUpdateCommandTestData(
                string moduleName,
                string moduleType,
                ImagePullPolicy imagePullPolicy,
                Dictionary<string, EnvVal> environmentVariables,
                string deviceId,
                string moduleId,
                string edgeletWorkloadUri,
                string moduleGenerationId,
                string edgeletAuthScheme,
                string iotHubHostname,
                string edgeDeviceHostname,
                Option<string> parentEdgeHostname,
                string upstreamProtocol,
                string edgeletManagementUri,
                string edgeletWorkloadListenUriMnt,
                string networkId,
                string edgeletApiVersion,
                object settings)
            {
                this.ModuleName = Preconditions.CheckNonWhiteSpace(moduleName, nameof(moduleName));
                this.ModuleType = Preconditions.CheckNonWhiteSpace(moduleType, nameof(moduleType));
                this.ImagePullPolicy = imagePullPolicy;
                this.EnvironmentVariables = Preconditions.CheckNotNull(environmentVariables, nameof(environmentVariables));
                this.DeviceId = Preconditions.CheckNonWhiteSpace(deviceId, nameof(deviceId));
                this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
                this.EdgeletWorkloadUri = Preconditions.CheckNonWhiteSpace(edgeletWorkloadUri, nameof(edgeletWorkloadUri));
                this.ModuleGenerationId = Preconditions.CheckNonWhiteSpace(moduleGenerationId, nameof(moduleGenerationId));
                this.EdgeletAuthScheme = Preconditions.CheckNonWhiteSpace(edgeletAuthScheme, nameof(edgeletAuthScheme));
                this.IoTHubHostname = Preconditions.CheckNonWhiteSpace(iotHubHostname, nameof(iotHubHostname));
                this.EdgeDeviceHostname = Preconditions.CheckNonWhiteSpace(edgeDeviceHostname, nameof(edgeDeviceHostname));
                this.ParentEdgeHostname = parentEdgeHostname;
                this.UpstreamProtocol = Preconditions.CheckNonWhiteSpace(upstreamProtocol, nameof(upstreamProtocol));
                this.EdgeletManagementUri = Preconditions.CheckNonWhiteSpace(edgeletManagementUri, nameof(edgeletManagementUri));
                this.EdgeletWorkloadListenUriMnt = Preconditions.CheckNonWhiteSpace(edgeletWorkloadListenUriMnt, nameof(edgeletWorkloadListenUriMnt));
                this.NetworkId = Preconditions.CheckNonWhiteSpace(networkId, nameof(networkId));
                this.EdgeletApiVersion = Preconditions.CheckNonWhiteSpace(edgeletApiVersion, nameof(edgeletApiVersion));
                this.Settings = settings;

                this.ModuleCredentials = new IdentityProviderServiceCredentials(this.EdgeletWorkloadUri, this.ModuleGenerationId, this.EdgeletAuthScheme);
            }

            internal string ModuleName { get; }

            internal string ModuleType { get; }

            internal ImagePullPolicy ImagePullPolicy { get; }

            internal Dictionary<string, EnvVal> EnvironmentVariables { get; }

            internal string DeviceId { get; }

            internal string ModuleId { get; }

            internal string EdgeletWorkloadUri { get; }

            internal string ModuleGenerationId { get; }

            internal string EdgeletAuthScheme { get; }

            internal ICredentials ModuleCredentials { get; }

            internal string IoTHubHostname { get; }

            internal string EdgeDeviceHostname { get; }

            internal Option<string> ParentEdgeHostname { get; }

            internal string UpstreamProtocol { get; }

            internal string EdgeletManagementUri { get; }

            internal string EdgeletWorkloadListenUriMnt { get; }

            internal string NetworkId { get; }

            internal string EdgeletApiVersion { get; }

            internal object Settings { get; }
        }
    }
}
