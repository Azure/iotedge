// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.IoTHub.SdkClient;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Xunit;
    using IotHubConnectionStringBuilder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder;

    class SdkModuleClientProviderHangFirstOpen : ISdkModuleClientProvider
    {
        FailingConnectionCounter failingConnectionCounter;

        public SdkModuleClientProviderHangFirstOpen()
        {
            this.failingConnectionCounter = new FailingConnectionCounter();
        }

        public ISdkModuleClient GetSdkModuleClient(string connectionString, ITransportSettings settings, TimeSpan cloudConnectionHangingTimeout)
        {
            ModuleClient moduleClient = ModuleClient.CreateFromConnectionString(connectionString, new[] { settings });
            WrappingSdkModuleClient wrappingSdkModuleClient = new WrappingSdkModuleClient(moduleClient, cloudConnectionHangingTimeout);
            return new WrappingSdkModuleClientHangFirstOpen(wrappingSdkModuleClient, this.failingConnectionCounter);
        }

        public async Task<ISdkModuleClient> GetSdkModuleClient(ITransportSettings settings, TimeSpan cloudConnectionHangingTimeout)
        {
            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync(new[] { settings });
            WrappingSdkModuleClient wrappingSdkModuleClient = new WrappingSdkModuleClient(moduleClient, cloudConnectionHangingTimeout);
            return new WrappingSdkModuleClientHangFirstOpen(wrappingSdkModuleClient, this.failingConnectionCounter);
        }
    }

    class FailingConnectionCounter
    {
        int failingConnectionCounter;

        public void Increment()
        {
            this.failingConnectionCounter += 1;
        }

        public int Value()
        {
            return this.failingConnectionCounter;
        }
    }

    class WrappingSdkModuleClientHangFirstOpen : ISdkModuleClient
    {
        readonly WrappingSdkModuleClient wrappingSdkModuleClient;
        FailingConnectionCounter failingConnectionCounter;

        public WrappingSdkModuleClientHangFirstOpen(WrappingSdkModuleClient wrappingSdkModuleClient, FailingConnectionCounter failingConnectionCounter)
        {
            this.wrappingSdkModuleClient = Preconditions.CheckNotNull(wrappingSdkModuleClient, nameof(wrappingSdkModuleClient));
            this.failingConnectionCounter = failingConnectionCounter;
        }

        public Task OpenAsync()
        {
            if (this.failingConnectionCounter.Value() == 0)
            {
                this.failingConnectionCounter.Increment();
                throw new EdgeAgentCloudSDKException("Operation timed out due to SDK hanging");
            }
            else
            {
                return this.wrappingSdkModuleClient.OpenAsync();
            }
        }

        public void SetConnectionStatusChangesHandler(ConnectionStatusChangesHandler statusChangesHandler)
            => this.wrappingSdkModuleClient.SetConnectionStatusChangesHandler(statusChangesHandler);

        public void SetOperationTimeoutInMilliseconds(uint operationTimeoutInMilliseconds)
            => this.wrappingSdkModuleClient.SetOperationTimeoutInMilliseconds(operationTimeoutInMilliseconds);

        public void SetProductInfo(string productInfo) => this.wrappingSdkModuleClient.SetProductInfo(productInfo);

        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyChanged)
            => this.wrappingSdkModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertyChanged);

        public Task SetMethodHandlerAsync(string methodName, MethodCallback callback)
            => this.wrappingSdkModuleClient.SetMethodHandlerAsync(methodName, callback);

        public Task SetDefaultMethodHandlerAsync(MethodCallback callback)
            => this.wrappingSdkModuleClient.SetDefaultMethodHandlerAsync(callback);

        public Task<Twin> GetTwinAsync()
        {
            return this.wrappingSdkModuleClient.GetTwinAsync();
        }

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties)
            => this.wrappingSdkModuleClient.UpdateReportedPropertiesAsync(reportedProperties);

        public Task SendEventAsync(Message message) => this.wrappingSdkModuleClient.SendEventAsync(message);

        public Task CloseAsync()
        {
            return this.wrappingSdkModuleClient.CloseAsync();
        }
    }

    public class EdgeAgentHangingConnectionTest
    {
        const string DockerType = "docker";
        static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(60);

        [Integration]
        [Fact]
        public async Task EdgeAgentConnectionHangInitialConnection()
        {
            string iotHubConnectionString = await SecretsHelper.GetSecretFromConfigKey("iotHubConnStrKey");
            IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(iotHubConnectionString);
            RegistryManager registryManager = RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            await registryManager.OpenAsync();

            string edgeDeviceId = "testMmaEdgeDevice1" + Guid.NewGuid();

            var edgeDevice = new Device(edgeDeviceId)
            {
                Capabilities = new DeviceCapabilities { IotEdge = true },
                Authentication = new AuthenticationMechanism() { Type = AuthenticationType.Sas }
            };

            try
            {
                edgeDevice = await registryManager.AddDeviceAsync(edgeDevice);

                await EdgeAgentConnectionTest.SetAgentDesiredProperties(registryManager, edgeDeviceId);

                var moduleClientProvider = new SdkModuleClientProviderHangFirstOpen();
                var edgeAgentConnection = EdgeAgentConnectionTest.CreateEdgeAgentConnection(iotHubConnectionStringBuilder, edgeDeviceId, edgeDevice, moduleClientProvider);

                await Task.Delay(TimeSpan.FromSeconds(10));

                Option<DeploymentConfigInfo> deploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

                Assert.True(deploymentConfigInfo.HasValue);
                DeploymentConfig deploymentConfig = deploymentConfigInfo.OrDefault().DeploymentConfig;
                Assert.NotNull(deploymentConfig);
                Assert.NotNull(deploymentConfig.Modules);
                Assert.NotNull(deploymentConfig.Runtime);
                Assert.NotNull(deploymentConfig.SystemModules);
                Assert.Equal(EdgeAgentConnection.ExpectedSchemaVersion.ToString(), deploymentConfig.SchemaVersion);
                Assert.Equal(1, deploymentConfig.Modules.Count);
                Assert.NotNull(deploymentConfig.Modules["mongoserver"]);
                EdgeAgentConnectionTest.ValidateRuntimeConfig(deploymentConfig.Runtime);
                EdgeAgentConnectionTest.ValidateModules(deploymentConfig);

                await EdgeAgentConnectionTest.UpdateAgentDesiredProperties(registryManager, edgeDeviceId);
                await Task.Delay(TimeSpan.FromSeconds(10));

                deploymentConfigInfo = await edgeAgentConnection.GetDeploymentConfigInfoAsync();

                Assert.True(deploymentConfigInfo.HasValue);
                deploymentConfig = deploymentConfigInfo.OrDefault().DeploymentConfig;
                Assert.NotNull(deploymentConfig);
                Assert.NotNull(deploymentConfig.Modules);
                Assert.NotNull(deploymentConfig.Runtime);
                Assert.NotNull(deploymentConfig.SystemModules);
                Assert.Equal(EdgeAgentConnection.ExpectedSchemaVersion.ToString(), deploymentConfig.SchemaVersion);
                Assert.Equal(2, deploymentConfig.Modules.Count);
                Assert.NotNull(deploymentConfig.Modules["mongoserver"]);
                Assert.NotNull(deploymentConfig.Modules["mlModule"]);
                EdgeAgentConnectionTest.ValidateRuntimeConfig(deploymentConfig.Runtime);
            }
            finally
            {
                try
                {
                    await registryManager.RemoveDeviceAsync(edgeDevice);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
    }
}