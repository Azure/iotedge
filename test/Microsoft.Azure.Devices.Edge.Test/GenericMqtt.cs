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
        const string GenericMqttInitiatorModuleName = "GenericMqttInitiator";
        const string GenericMqttRelayerModuleName = "GenericMqttRelayer";
        const string GenericMqttTesterMaxMessages = "5";
        const string GenericMqttTesterTestStartDelay = "10s";
        const int SecondsBeforeVerification = 45;

        /// <summary>
        /// Scenario:
        /// - Deploy Edge Hub with the broker enabled and two genericMqttTester
        ///   modules.
        /// - One genericMqttTester module will be in initiate mode and the other
        ///   will be in relay mode.
        /// - The initiate mode module will publish on an mqtt topic initiate/1
        ///   which the relaying module will be subscribed to.
        /// - When the relaying module receives a message, it will publish it
        ///   back on relay/1.
        /// - The initiating module will be subscribed on this relay/1 topic
        ///   and will report to the TRC when it receives the message.
        /// </summary>
        /// <returns><see cref="Task"/> representing the asynchronous unit test.</returns>
        [Test]
        [Category("Flaky")]
        [Category("BrokerRequired")]
        public async Task GenericMqttTelemetry()
        {
            CancellationToken token = this.TestToken;
            string networkControllerImage = Context.Current.NetworkControllerImage.Expect(() => new ArgumentException("networkControllerImage parameter is required for Generic Mqtt test"));
            string trcImage = Context.Current.TestResultCoordinatorImage.Expect(() => new ArgumentException("testResultCoordinatorImage parameter is required for Generic Mqtt test"));
            string genericMqttTesterImage = Context.Current.GenericMqttTesterImage.Expect(() => new ArgumentException("genericMqttTesterImage parameter is required for Generic Mqtt test"));
            string trackingId = Guid.NewGuid().ToString();

            Action<EdgeConfigBuilder> addMqttBrokerConfig = MqttBrokerUtil.BuildAddBrokerToDeployment(false);
            Action<EdgeConfigBuilder> addNetworkControllerConfig = TestResultCoordinatorUtil.BuildAddNetworkControllerConfig(trackingId, networkControllerImage);
            Action<EdgeConfigBuilder> addTestResultCoordinatorConfig = TestResultCoordinatorUtil.BuildAddTestResultCoordinatorConfig(trackingId, trcImage, GenericMqttInitiatorModuleName, GenericMqttInitiatorModuleName);
            Action<EdgeConfigBuilder> addGenericMqttTesterConfig = this.BuildAddGenericMqttTesterConfig(trackingId, trcImage, genericMqttTesterImage);
            Action<EdgeConfigBuilder> config = addMqttBrokerConfig + addNetworkControllerConfig + addTestResultCoordinatorConfig + addGenericMqttTesterConfig;
            EdgeDeployment deployment = await this.runtime.DeployConfigurationAsync(config, token, Context.Current.NestedEdge);

            await Task.Delay(TimeSpan.FromSeconds(SecondsBeforeVerification));

            await TestResultCoordinatorUtil.ValidateResultsAsync();
        }

        private Action<EdgeConfigBuilder> BuildAddGenericMqttTesterConfig(string trackingId, string trcImage, string genericMqttTesterImage)
        {
            return new Action<EdgeConfigBuilder>(
                builder =>
                {
                    builder.AddModule(GenericMqttInitiatorModuleName, genericMqttTesterImage, false)
                        .WithEnvironment(new[]
                        {
                            ("TEST_SCENARIO", "InitiateAndReceiveRelayed"),
                            ("TRACKING_ID", trackingId),
                            ("TEST_START_DELAY", GenericMqttTesterTestStartDelay),
                            ("MESSAGES_TO_SEND", GenericMqttTesterMaxMessages),
                        });

                    builder.AddModule(GenericMqttRelayerModuleName, genericMqttTesterImage, false)
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
