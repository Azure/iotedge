// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;

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
