// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Reports.EdgeHubRestartTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util;

    class EdgeHubRestartStatistics
    {
        public EdgeHubRestartStatistics(Dictionary<HttpStatusCode, List<TimeSpan>> completedStatusHistogram)
        {
            this.CompletedStatusHistogram = Preconditions.CheckNotNull(completedStatusHistogram, nameof(completedStatusHistogram));
            this.MinPeriod = TimeSpan.FromTicks(0);
            this.MaxPeriod = TimeSpan.FromTicks(0);
            this.MedianPeriod = TimeSpan.FromTicks(0);
            this.MeanPeriod = TimeSpan.FromTicks(0);
            this.VariancePeriodInMilisec = 0.0;
        }

        public TimeSpan MinPeriod { get; private set; }

        public TimeSpan MaxPeriod { get; private set; }

        public TimeSpan MedianPeriod { get; private set; }

        public TimeSpan MeanPeriod { get; private set; }

        public double VariancePeriodInMilisec { get; private set; }

        public Dictionary<HttpStatusCode, List<TimeSpan>> CompletedStatusHistogram { get; }

        public void CalculateStatistic()
        {
            List<TimeSpan> completedPeriods;
            this.CompletedStatusHistogram.TryGetValue(HttpStatusCode.OK, out completedPeriods);
            List<TimeSpan> orderedCompletedPeriods = completedPeriods?.OrderBy(p => p.Ticks).ToList();

            if (orderedCompletedPeriods != null)
            {
                this.MinPeriod = orderedCompletedPeriods.First();
                this.MaxPeriod = orderedCompletedPeriods.Last();

                if ((orderedCompletedPeriods.Count & 0b1) == 0b1)
                {
                    // If odd, pick the middle value
                    this.MedianPeriod = orderedCompletedPeriods[orderedCompletedPeriods.Count >> 1];
                }
                else
                {
                    // If even, average the middle values
                    this.MedianPeriod =
                        (orderedCompletedPeriods[orderedCompletedPeriods.Count >> 1] +
                        orderedCompletedPeriods[(orderedCompletedPeriods.Count >> 1) - 1]) / 2;
                }

                // Compute Mean
                TimeSpan totalSpan = TimeSpan.FromTicks(0);
                double totalSpanSquareInMilisec = 0.0;
                foreach (TimeSpan eachTimeSpan in orderedCompletedPeriods)
                {
                    totalSpan += eachTimeSpan;
                    totalSpanSquareInMilisec += Math.Pow(eachTimeSpan.TotalMilliseconds, 2);
                }

                // Compute Mean : mean = sum(x) / N
                this.MeanPeriod = totalSpan / Math.Max(orderedCompletedPeriods.Count(), 1);

                // Compute Sample Variance: var = sum((x - mean)^2) / (N - 1)
                //                              = sum(x^2) / (N - 1) - mean^2
                this.VariancePeriodInMilisec = (totalSpanSquareInMilisec / Math.Max(orderedCompletedPeriods.Count() - 1, 1)) - Math.Pow(this.MeanPeriod.TotalMilliseconds, 2);
            }
        }
    }
}