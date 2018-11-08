// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.ProtocolGateway;
    using Microsoft.Extensions.Configuration;

    public class MqttSettingsProvider : ISettingsProvider
    {
        readonly IConfiguration mqttSettings;

        public MqttSettingsProvider(IConfiguration mqttSettings)
        {
            this.mqttSettings = Preconditions.CheckNotNull(mqttSettings, nameof(mqttSettings));
        }

        public bool TryGetSetting(string name, out string value)
        {
            // GetValue will return a null if the key is not present
            value = this.mqttSettings.GetValue<string>(name);
            return value != null;
        }
    }
}
