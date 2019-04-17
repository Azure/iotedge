// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Edge.Util;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace common
{
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
                $"Waiting for expected twin updates for module '{moduleId}'",
                () => {
                    return Retry.Do(
                        async () => {
                            Twin twin = await this.iotHub.GetTwinAsync(this.deviceId, this.moduleId, token);
                            // TODO: Only newer versions of tempSensor mirror certain desired properties
                            //       to reported (e.g. 1.0.7-rc2). So return desired properties until
                            //       the temp-sensor e2e test can pull docker images other than the
                            //       default 'mcr...:1.0'.
                            // return twin.Properties.Reported;
                            return twin.Properties.Desired;
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