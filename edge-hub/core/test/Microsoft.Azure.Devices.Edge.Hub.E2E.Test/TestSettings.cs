// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    public class TestSettings
    {
        public static readonly IotHubClientOptions MqttClientOptions = new IotHubClientOptions(
            new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            });

        public static readonly IotHubClientOptions AmqpClientOptions = new IotHubClientOptions(
            new IotHubClientAmqpSettings(IotHubClientTransportProtocol.Tcp)
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
            });

        public static readonly IotHubClientOptions MqttWebSocketsClientOptions = new IotHubClientOptions(
            new IotHubClientMqttSettings(IotHubClientTransportProtocol.WebSocket));

        public static readonly IotHubClientOptions AmqpWebSocketsClientOptions = new IotHubClientOptions(
            new IotHubClientAmqpSettings(IotHubClientTransportProtocol.WebSocket));

        static readonly Lazy<IList<object[]>> TransportSettingsLazy = new Lazy<IList<object[]>>(() => GetTransportSettings(), true);

        public static IList<object[]> TransportSettings => TransportSettingsLazy.Value;

        static readonly Lazy<IList<object[]>> AmqpTransportTestSettingsLazy = new Lazy<IList<object[]>>(() => GetAmqpTransportSettings(false), true);

        public static IList<object[]> AmqpTransportTestSettings => AmqpTransportTestSettingsLazy.Value;

        static IList<object[]> GetTransportSettings()
        {
            bool.TryParse(ConfigHelper.TestConfig["enableWebSocketsTests"], out bool enableWebSocketsTests);
            IList<object[]> transportSettings = GetAmqpTransportSettings(enableWebSocketsTests);

            transportSettings.Add(new object[] { MqttClientOptions });
            if (enableWebSocketsTests)
            {
                transportSettings.Add(new object[] { MqttWebSocketsClientOptions });
            }

            return transportSettings;
        }

        static IList<object[]> GetAmqpTransportSettings(bool webSockets = false)
        {
            IList<object[]> transportSettings = new List<object[]>
            {
                new object[] { AmqpClientOptions },
            };

            if (webSockets)
            {
                transportSettings.Add(new object[] { AmqpWebSocketsClientOptions });
            }

            return transportSettings;
        }
    }
}
