// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json.Linq;

    public class ModuleTwin
    {
        string deviceId;
        IotHub iotHub;
        string moduleId;

        public ModuleTwin(string moduleId, string deviceId, IotHub iotHub)
        {
            this.deviceId = deviceId;
            this.iotHub = iotHub;
            this.moduleId = moduleId;
        }

        public Task UpdateDesiredPropertiesAsync(object patch, CancellationToken token)
        {
            return Profiler.Run(
                $"Updating twin for module '{this.moduleId}'",
                () => this.iotHub.UpdateTwinAsync(this.deviceId, this.moduleId, patch, token)
            );
        }

        public Task WaitForReportedPropertyUpdatesAsync(object expectedPatch, CancellationToken token)
        {
            return Profiler.Run(
                $"Waiting for expected twin updates for module '{this.moduleId}'",
                () => {
                    return Retry.Do(
                        async () => {
                            Twin twin = await this.iotHub.GetTwinAsync(this.deviceId, this.moduleId, token);
                            return twin.Properties.Reported;
                        },
                        reported => {
                            JObject expected = JObject.FromObject(expectedPatch)
                                .Value<JObject>("properties")
                                .Value<JObject>("reported");
                            return expected.Value<JObject>().All<KeyValuePair<string, JToken>>(
                                prop => reported.Contains(prop.Key) && reported[prop.Key] == prop.Value
                            );
                        },
                        null,
                        TimeSpan.FromSeconds(5),
                        token
                    );
                }
            );
        }
    }
}
