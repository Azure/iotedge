// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test.Util
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util;
    using Xunit;

    public class TimeSeriesHelperTest
    {
        Random rand = new Random();

        [Fact]
        public void TestCondenseTimeSeries()
        {
            /* fake data */
            DateTime baseTime = new DateTime(10000000, DateTimeKind.Utc);
            int start = 10;
            int n = 100; // Keep this value even

            // baseline
            Metric[] scrape1 = Enumerable.Range(start, n).Select(i => new Metric(baseTime, $"Test Metric {i}", i, $"Tags")).ToArray();

            // second half is changed
            Metric[] scrape2_1 = Enumerable.Range(start, n / 2).Select(i => new Metric(baseTime, $"Test Metric {i}", i, $"Tags")).ToArray();
            Metric[] scrape2_2 = Enumerable.Range(start + n / 2, n / 2).Select(i => new Metric(baseTime, $"Test Metric {i}", i * 2, $"Tags")).ToArray();
            Metric[] scrape2 = scrape2_1.Concat(scrape2_2).ToArray();

            // everything changed
            Metric[] scrape3 = Enumerable.Range(start, n).Select(i => new Metric(baseTime, $"Test Metric {i}", i / 2.0, $"Tags")).ToArray();

            /* test */
            IEnumerable<Metric> test1 = scrape1.Concat(scrape1).Concat(scrape1).Concat(scrape1);
            IEnumerable<Metric> result1 = TimeSeriesHelper.CondenseTimeSeries(test1);
            Assert.Equal(n * 2, result1.Count()); // Keeps the first and last results

            IEnumerable<Metric> test2 = scrape1.Concat(scrape1).Concat(scrape1).Concat(scrape2).Concat(scrape2);
            IEnumerable<Metric> result2 = TimeSeriesHelper.CondenseTimeSeries(test2);
            Assert.Equal(n * 2 + n, result2.Count()); // Keeps the first and last results of the baseline, and the first and last results of the second half of the second scrape

            IEnumerable<Metric> test3 = scrape1.Concat(scrape1).Concat(scrape1).Concat(scrape1).Concat(scrape3);
            IEnumerable<Metric> result3 = TimeSeriesHelper.CondenseTimeSeries(test3);
            Assert.Equal(n * 2 + n, result3.Count()); // Keeps the first and last results of the baseline, and the results of the of the third scrape

            IEnumerable<Metric> test4 = scrape1.Concat(scrape3);
            IEnumerable<Metric> result4 = TimeSeriesHelper.CondenseTimeSeries(test4);
            Assert.Equal(n + n, result4.Count()); // Keeps the baseline, and the results of the of the third scrape

            IEnumerable<Metric> test5 = scrape1.Concat(scrape2).Concat(scrape2).Concat(scrape2).Concat(scrape3);
            IEnumerable<Metric> result5 = TimeSeriesHelper.CondenseTimeSeries(test5);
            int fromScrape1 = n; // one result from first scrape.
            int fromScrape2 = n / 2 + n; // initial change catches half, final change gets all
            int fromScrape3 = n; // Changes all
            Assert.Equal(fromScrape1 + fromScrape2 + fromScrape3, result5.Count());
        }

        [Fact]
        public void TestCondenseTimeSeriesKeepsLine()
        {
            DateTime baseTime = new DateTime(10000000, DateTimeKind.Utc);

            Metric[] testMetrics = new Metric[]
            {
                new Metric(baseTime, "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(1), "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(2), "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(3), "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(4), "Test", 2, "Tags"),
                new Metric(baseTime.AddMinutes(5), "Test", 3, "Tags"),
                new Metric(baseTime.AddMinutes(6), "Test", 3, "Tags"),
                new Metric(baseTime.AddMinutes(7), "Test", 3, "Tags"),
                new Metric(baseTime.AddMinutes(8), "Test", 3, "Tags"),
                new Metric(baseTime.AddMinutes(9), "Test", 3, "Tags"),
            };

            Metric[] expected = new Metric[]
            {
                new Metric(baseTime, "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(3), "Test", 1, "Tags"),
                new Metric(baseTime.AddMinutes(4), "Test", 2, "Tags"),
                new Metric(baseTime.AddMinutes(5), "Test", 3, "Tags"),
                new Metric(baseTime.AddMinutes(9), "Test", 3, "Tags"),
            };

            Metric[] result = testMetrics.CondenseTimeSeries().ToArray();
            Assert.Equal(expected, result);
        }
    }
}
