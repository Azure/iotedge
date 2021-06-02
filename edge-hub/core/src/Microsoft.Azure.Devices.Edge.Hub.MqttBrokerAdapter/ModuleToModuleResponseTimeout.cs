// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;

    // The role of this class is to help injecting a timeout value without activators
    public class ModuleToModuleResponseTimeout
    {
        TimeSpan timeout;

        public ModuleToModuleResponseTimeout(TimeSpan timeout)
        {
            this.timeout = timeout;
        }

        public static implicit operator TimeSpan(ModuleToModuleResponseTimeout t) => t.timeout;
    }
}
