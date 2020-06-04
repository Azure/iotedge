// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MetricsValidator.Util;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class ValidateDocumentedMetrics : TestBase
    {
        public ValidateDocumentedMetrics(TestReporter testReporter, IMetricsScraper scraper, ModuleClient moduleClient)
            : base(testReporter, scraper, moduleClient)
        {
        }

        protected override async Task Test(CancellationToken cancellationToken)
        {
            await this.SeedMetrics(cancellationToken);
            bool hostMetricsFound = await HostMetricUtil.WaitForHostMetrics(this.scraper, cancellationToken);
            this.testReporter.Assert("Host Metrics Found", hostMetricsFound);

            // scrape metrics
            var metrics = await this.scraper.ScrapeEndpointsAsync(cancellationToken);

            var expected = this.GetExpectedMetrics();
            if (RuntimeInformation.OSArchitecture == Architecture.Arm || RuntimeInformation.OSArchitecture == Architecture.Arm64)
            {
                // Docker doesn't return this on arm
                expected.Remove("edgeAgent_created_pids_total");
            }

            if (OsPlatform.IsWindows())
            {
                // Docker doesn't return this on windows
                expected.Remove("edgeAgent_created_pids_total");
            }

            HashSet<string> unreturnedMetrics = new HashSet<string>(expected.Keys);
            if (expected.Count == 0)
            {
                this.testReporter.Assert("No documented metrics", false, string.Empty);
                return;
            }

            log.LogInformation("Got expected metrics");
            foreach (Metric metric in metrics)
            {
                unreturnedMetrics.Remove(metric.Name);

                string metricId = $"{metric.Name}:{JsonConvert.SerializeObject(metric.Tags)}";
                if (expected.TryGetValue(metric.Name, out ExpectedMetric expectedMetric))
                {
                    // Make sure all tags are returned
                    this.testReporter.Assert($"{metricId} tags", expectedMetric.Tags.All(metric.Tags.ContainsKey), $"Expected metric {metric.Name} to contain tags {string.Join(", ", expectedMetric.Tags)}.\nActual tags: {JsonConvert.SerializeObject(metric.Tags)}");

                    // Make sure values are in bounds
                    if (expectedMetric.Bounds != null)
                    {
                        this.testReporter.Assert($"{metricId} value", expectedMetric.Tags.All(metric.Tags.ContainsKey), $"Expected metric {metric.Name} to contain tags {string.Join(", ", expectedMetric.Tags)}.\nActual tags: {JsonConvert.SerializeObject(metric.Tags)}");
                    }
                }
                else
                {
                    this.testReporter.Assert(metricId, false, "Metric is not documented.");
                }
            }

            foreach (string unreturnedMetric in unreturnedMetrics)
            {
                this.testReporter.Assert(unreturnedMetric, false, $"Metric did not exist in scrape.");
            }
        }

        Dictionary<string, ExpectedMetric> GetExpectedMetrics()
        {
            try
            {
                var lines = File.ReadAllLines(Path.Combine("doc", "BuiltInMetrics.md"));
                return ExpectedMetric.ParseDoc(lines).ToDictionary(m => m.Name, m => m);
            }
            catch (Exception ex)
            {
                this.testReporter.Assert("Read metrics docs", false, ex.Message);
                return new Dictionary<string, ExpectedMetric>();
            }
        }

        class ExpectedMetric
        {
            static readonly Regex Word = new Regex(@"`(\w*)`", RegexOptions.Compiled);
            static readonly Regex Any = new Regex(@"`(.*)`", RegexOptions.Compiled);

            public string Name { get; }
            public string[] Tags { get; }
            public string Type { get; }
            public RangeBound Bounds { get; }

            ExpectedMetric(string name, string[] tags, string type, RangeBound bounds)
            {
                this.Name = name;
                this.Tags = tags;
                this.Type = type;
                this.Bounds = bounds;
            }

            public static IEnumerable<ExpectedMetric> ParseDoc(IEnumerable<string> lines)
            {
                var columns = lines.Select(line => line.Split('|'))
                    .Where(rows => rows.Length == 7);

                foreach (var column in columns)
                {
                    var nameMatch = Word.Match(column[1]);
                    var tagsMatch = Word.Matches(column[2]);
                    var typeMatch = Word.Match(column[4]);
                    var boundsMatch = Any.Match(column[5]);

                    if (nameMatch.Success && typeMatch.Success)
                    {
                        string name = nameMatch.Groups[1].Value;
                        string[] tags = tagsMatch.Select(match => match.Groups[1].Value).ToArray();
                        string type = typeMatch.Groups[1].Value;
                        RangeBound bounds = boundsMatch.Success ? RangeBound.ParseBound(boundsMatch.Value) : null;

                        if (type == "Histogram")
                        {
                            string[] sumCountTags = tags.Where(t => t != "quantile").ToArray();
                            yield return new ExpectedMetric(name + "_sum", sumCountTags, type, bounds);
                            yield return new ExpectedMetric(name + "_count", sumCountTags, type, bounds);
                        }

                        yield return new ExpectedMetric(name, tags, type, bounds);
                    }
                }
            }
        }

        class RangeBound
        {
            static readonly Regex Range = new Regex(@"`(?<lowerInclusive>[\[\(])(?<lowerBound>\-?\d*\.?\d*)\s*,\s*(?<upperBound>\-?\d*\.?\d*)(?<upperInclusive>[\]\)])`", RegexOptions.Compiled);

            bool LowerInclusive { get; }
            double LowerBound { get; }
            bool UpperInclusive { get; }
            double UpperBound { get; }
            string Referance { get; }

            RangeBound(bool lowerInclusive, double lowerBound, bool upperInclusive, double upperBound, string referance)
            {
                this.LowerInclusive = lowerInclusive;
                this.LowerBound = lowerBound;
                this.UpperInclusive = upperInclusive;
                this.UpperBound = upperBound;
                this.Referance = referance;
            }

            public static RangeBound ParseBound(string text)
            {
                var match = Range.Match(text);

                if (!match.Success)
                {
                    throw new Exception($"Could not parse range: {text}");
                }

                bool lowerInclusive = match.Groups["lowerInclusive"].Value == "[";
                double lowerBound = match.Groups["lowerBound"].Value == string.Empty ? double.MinValue : double.Parse(match.Groups["lowerBound"].Value);
                bool upperInclusive = match.Groups["upperInclusive"].Value == "]";
                double upperBound = match.Groups["upperBound"].Value == string.Empty ? double.MaxValue : double.Parse(match.Groups["upperBound"].Value);

                return new RangeBound(lowerInclusive, lowerBound, upperInclusive, upperBound, text);
            }

            public bool InRange(double value)
            {
                return this.LowerInclusive ? value >= this.LowerBound : value > this.LowerBound &&
                    this.UpperInclusive ? value <= this.UpperBound : value < this.UpperBound;
            }
        }

        async Task SeedMetrics(CancellationToken cancellationToken)
        {
            // Send at least 1 message
            await this.moduleClient.SendEventAsync(new Message(Encoding.UTF8.GetBytes("Test message to seed metrics")), cancellationToken);

            // Send and receive 1 direct method
            const string methodName = "FakeDirectMethod";
            await this.moduleClient.SetMethodHandlerAsync(methodName, (_, __) => Task.FromResult(new MethodResponse(200)), null);
            await this.moduleClient.InvokeMethodAsync(Environment.GetEnvironmentVariable("IOTEDGE_DEVICEID"), Environment.GetEnvironmentVariable("IOTEDGE_MODULEID"), new MethodRequest(methodName), cancellationToken);

            // Get at least 1 twin update
            await this.moduleClient.UpdateReportedPropertiesAsync(new TwinCollection(), cancellationToken);
            await this.moduleClient.GetTwinAsync(cancellationToken);

            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }
}
