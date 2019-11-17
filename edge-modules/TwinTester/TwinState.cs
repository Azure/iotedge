// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class TwinState
    {
        public int ReportedPropertyUpdateCounter;
        public int DesiredPropertyUpdateCounter;
        public string TwinETag;
        public DateTime LastTimeOffline;

        public TwinState(int reportedPropertyUpdateCounter, int desiredPropertyUpdateCounter, string twinETag, DateTime lastTimeOffline)
        {
            this.ReportedPropertyUpdateCounter = reportedPropertyUpdateCounter;
            this.DesiredPropertyUpdateCounter = desiredPropertyUpdateCounter;
            this.TwinETag = twinETag;
            this.LastTimeOffline = lastTimeOffline;
        }
    }
}
