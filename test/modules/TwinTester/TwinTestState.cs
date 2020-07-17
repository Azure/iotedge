// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    class TwinTestState
    {
        readonly AtomicLong edgeHubLastStarted = new AtomicLong();
        readonly AtomicLong edgeHubLastStopped = new AtomicLong();
        readonly AtomicLong lastNetworkOffline = new AtomicLong();

        public TwinTestState(string twinETag)
            : this(0, 0, twinETag, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue)
        {
        }

        public TwinTestState(
            int reportedPropertyUpdateCounter,
            int desiredPropertyUpdateCounter,
            string twinETag,
            DateTime lastNetworkOffline,
            DateTime edgeHubLastStarted,
            DateTime edgeHubLastStopped)
        {
            this.ReportedPropertyUpdateCounter = reportedPropertyUpdateCounter;
            this.DesiredPropertyUpdateCounter = desiredPropertyUpdateCounter;
            this.TwinETag = twinETag;
            this.edgeHubLastStarted.Set(edgeHubLastStarted.Ticks);
            this.edgeHubLastStopped.Set(edgeHubLastStopped.Ticks);
            this.lastNetworkOffline.Set(lastNetworkOffline.Ticks);
        }

        public int ReportedPropertyUpdateCounter { get; }

        public int DesiredPropertyUpdateCounter { get; }

        public string TwinETag { get; set; }

        public DateTime LastNetworkOffline
        {
            get => new DateTime(this.lastNetworkOffline.Get());
            set => this.lastNetworkOffline.Set(value.Ticks);
        }

        public DateTime EdgeHubLastStopped
        {
            get => new DateTime(this.edgeHubLastStopped.Get());
            set => this.edgeHubLastStopped.Set(value.Ticks);
        }

        public DateTime EdgeHubLastStarted
        {
            get => new DateTime(this.edgeHubLastStarted.Get());
            set => this.edgeHubLastStarted.Set(value.Ticks);
        }
    }
}
