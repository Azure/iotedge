// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;

    [EndToEnd]
    public class GenericMqtt : SasManualProvisioningFixture
    {
        const string NetworkControllerModuleName = "networkController";
        const string GenericMqttTesterModuleName = "GenericMqttTester";
        const int GenericMqttTesterMaxMessages = 10;
        const string GenericMqttTesterTestStartDelay = "45s";
        const int SecondsBeforeVerification = 90;

        [Test]
        public async Task PriorityQueueModuleToModuleMessages()
        {
            CancellationToken token = this.TestToken;
            string networkControllerImage = Context.Current.NetworkControllerImage.Expect(() => new ArgumentException("networkControllerImage parameter is required for Generic Mqtt test"));
            string trcImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("testResultCoordinatorImage parameter is required for Generic Mqtt test"));
            string genericMqttTesterImage = Context.Current.GenericMqttTesterImage.Expect(() => new ArgumentException("genericMqttTesterImage parameter is required for Generic Mqtt test"));
            string trackingId = Guid.NewGuid().ToString();

            Action<EdgeConfigBuilder> addNetworkControllerConfig = TestResultCoordinatorUtil.BuildAddNetworkControllerConfig(trackingId, networkControllerImage);
            Action<EdgeConfigBuilder> addTestResultCoordinatorConfig = TestResultCoordinatorUtil.BuildAddTestResultCoordinatorConfig(trackingId, trcImage, GenericMqttTesterModuleName, GenericMqttTesterModuleName);
            Action<EdgeConfigBuilder> addGenericMqttTesterConfig = this.BuildAddGenericMqttTesterConfig(trackingId, trcImage, genericMqttTesterImage);
            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(addNetworkControllerConfig + addTestResultCoordinatorConfig + addGenericMqttTesterConfig, token, Context.Current.NestedEdge);
            await Task.Delay(TimeSpan.FromSeconds(SecondsBeforeVerification));
            await TestResultCoordinatorUtil.ValidateResultsAsync();
        }

        private Action<EdgeConfigBuilder> BuildAddGenericMqttTesterConfig(string trackingId, string trcImage, string genericMqttTesterImage)
        {
            return new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(GenericMqttTesterModuleName, genericMqttTesterImage)
                        .WithEnvironment(new[]
                        {
                            ("TEST_SCENARIO", "Initiate"),
                            ("TRACKING_ID", trackingId),
                            ("TEST_START_DELAY", GenericMqttTesterTestStartDelay),
                        });

                    builder.AddModule(GenericMqttTesterModuleName, genericMqttTesterImage)
                        .WithEnvironment(new[]
                        {
                            ("TEST_SCENARIO", "Relay"),
                            ("TRACKING_ID", trackingId),
                            ("TEST_START_DELAY", GenericMqttTesterTestStartDelay),
                        });
                });
        }
    }
}

