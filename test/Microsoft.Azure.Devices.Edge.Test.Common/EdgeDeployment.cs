// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class EdgeDeployment
    {
        // StartTime represents the moment the configuration was sent to IoT Hub. Some of the test
        // modules begin sending events as soon as they are launched, so this timestamp can be used
        // as a reasonable starting point when listening for events on the IoT hub's Event Hub-
        // compatible endpoint.
        public DateTime StartTime { get; }

        public IReadOnlyDictionary<string, EdgeModule> Modules { get; }

        public EdgeDeployment(DateTime startTime, IEnumerable<EdgeModule> modules)
        {
            this.Modules = modules.ToDictionary(module => module.Id);
            this.StartTime = startTime;
        }
    }
}
