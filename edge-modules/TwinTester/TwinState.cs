// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;

    public class TwinState
    {
        public int ReportedPropertyUpdateCounter { get; set; }

        public int DesiredPropertyUpdateCounter { get; set; }

        public string TwinETag { get; set; }

        public DateTime LastTimeOffline { get; set; }
    }
}
