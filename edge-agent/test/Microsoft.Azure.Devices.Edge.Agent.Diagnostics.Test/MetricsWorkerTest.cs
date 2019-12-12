// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class MetricsWorkerTest : IDisposable
    {
        TempDirectory tempDirectory = new TempDirectory();

        [Fact]
        public async Task TestScraping()
        {
            /* test data */
            (string name, double value)[] modules = Enumerable.Range(1, 10).Select(i => ($"module_{i}", 1.0)).ToArray();

            /* Setup mocks */
            var scraper = new Mock<IMetricsScraper>();
            scraper.Setup(s => s.ScrapeEndpointsAsync(CancellationToken.None)).ReturnsAsync(() => this.PrometheousMetrics(modules));

            var storage = new Mock<IMetricsStorage>();
            string storedValue = string.Empty;
            storage.Setup(s => s.WriteData(It.IsAny<string>())).Callback((Action<string>)(data => storedValue = data));

            var uploader = new Mock<IMetricsPublisher>();

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            // single value is stored
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(1, scraper.Invocations.Count);
            Assert.Equal(1, storage.Invocations.Count);

            // duplicates don't get stored
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(2, scraper.Invocations.Count);
            Assert.Equal(1, storage.Invocations.Count);

            modules[1].value = 2;
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(3, scraper.Invocations.Count);
            Assert.Equal(2, storage.Invocations.Count);
            Assert.Contains("module_2", storedValue);
            Assert.Contains("1", storedValue);

            // multiple values get stored
            modules[1].value = 3;
            modules[2].value = 3;
            modules[7].value = 3;
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(4, scraper.Invocations.Count);
            Assert.Equal(3, storage.Invocations.Count);
            Assert.Contains("module_2", storedValue);
            Assert.Contains("3", storedValue);

            // multiple duplicates don't get stored
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(5, scraper.Invocations.Count);
            Assert.Equal(3, storage.Invocations.Count);
        }

        [Fact]
        public async Task TestBasicUploading()
        {
            /* Setup mocks */
            var scraper = new Mock<IMetricsScraper>();

            var storage = new Mock<IMetricsStorage>();
            storage.Setup(s => s.GetData(It.IsAny<DateTime>())).Returns(new Dictionary<DateTime, Func<string>>
            {
                { DateTime.UtcNow, () => string.Empty }
            });

            var uploader = new Mock<IMetricsPublisher>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), It.IsAny<CancellationToken>())).Callback((Action<IEnumerable<Metric>, CancellationToken>)((data, __) => uploadedData = data)).Returns(Task.CompletedTask);

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            /* test */
            await worker.Upload(CancellationToken.None);
            uploadedData.ToList();
            Assert.Single(storage.Invocations.Where(s => s.Method.Name == "GetData"));
            Assert.Equal(1, uploader.Invocations.Count);
        }

        [Fact]
        public async Task TestUploadContent()
        {
            /* test data */
            var metrics = Enumerable.Range(1, 10).Select(i => new Metric(DateTime.UtcNow, "test_metric", 3, $"tag_{i}")).ToList();

            /* Setup mocks */
            var scraper = new Mock<IMetricsScraper>();

            var storage = new Mock<IMetricsStorage>();
            storage.Setup(s => s.GetData(It.IsAny<DateTime>())).Returns(new Dictionary<DateTime, Func<string>>
            {
                { DateTime.UtcNow, () => Newtonsoft.Json.JsonConvert.SerializeObject(metrics) }
            });
            var uploader = new Mock<IMetricsPublisher>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), It.IsAny<CancellationToken>())).Callback((Action<IEnumerable<Metric>, CancellationToken>)((d, _) => uploadedData = d)).Returns(Task.CompletedTask);

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            /* test */
            await worker.Upload(CancellationToken.None);
            Assert.Equal(metrics.OrderBy(x => x.Tags), uploadedData.OrderBy(x => x.Tags));
            Assert.Single(storage.Invocations.Where(s => s.Method.Name == "GetData"));
            Assert.Equal(1, uploader.Invocations.Count);
        }

        [Fact]
        public async Task TestUploadIsLazy()
        {
            /* test data */
            int metricsCalls = 0;
            string Metrics()
            {
                metricsCalls++;
                return Newtonsoft.Json.JsonConvert.SerializeObject(Enumerable.Range(1, 10).Select(i => new Metric(DateTime.UtcNow, "1", 3, $"{i}")));
            }

            Dictionary<DateTime, Func<string>> data = Enumerable.Range(1, 10).ToDictionary(i => new DateTime(i * 100000000, DateTimeKind.Utc), _ => (Func<string>)Metrics);

            /* Setup mocks */
            var scraper = new Mock<IMetricsScraper>();

            var storage = new Mock<IMetricsStorage>();
            storage.Setup(s => s.GetData(It.IsAny<DateTime>())).Returns(data);

            var uploader = new Mock<IMetricsPublisher>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), It.IsAny<CancellationToken>())).Callback((Action<IEnumerable<Metric>, CancellationToken>)((d, _) => uploadedData = d)).Returns(Task.CompletedTask);

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            /* test */
            await worker.Upload(CancellationToken.None);
            int numMetrics = 0;
            foreach (Metric metric in uploadedData)
            {
                Assert.Equal(numMetrics++ / 10 + 1, metricsCalls);
            }

            Assert.Single(storage.Invocations.Where(s => s.Method.Name == "GetData"));
            Assert.Equal(1, uploader.Invocations.Count);
        }

        [Fact]
        public async Task TestScrapeAndUpload()
        {
            /* Setup mocks */
            var systemTime = new Mock<ISystemTime>();
            DateTime fakeTime = new DateTime(100000000, DateTimeKind.Utc);
            systemTime.Setup(x => x.UtcNow).Returns(() => fakeTime);

            CancellationToken ct = CancellationToken.None;

            var scraper = new Mock<IMetricsScraper>();
            var scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 1.0)));
            scraper.Setup(s => s.ScrapeEndpointsAsync(ct)).ReturnsAsync(() => scrapeResults);

            var storage = new MetricsFileStorage(this.tempDirectory.CreateTempDir(), systemTime.Object);

            var uploader = new Mock<IMetricsPublisher>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), ct)).Callback((Action<IEnumerable<Metric>, CancellationToken>)((d, _) => uploadedData = d.ToArray())).Returns(Task.CompletedTask);

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage, uploader.Object, systemTime.Object);

            /* test without de-duping */
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 1.0)));
            await worker.Scrape(ct);
            fakeTime = fakeTime.AddMinutes(10);
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 2.0)));
            await worker.Scrape(ct);
            fakeTime = fakeTime.AddMinutes(1);
            await worker.Upload(ct);
            Assert.Equal(20, uploadedData.Count());
            fakeTime = fakeTime.AddMinutes(1);
            await worker.Upload(ct);
            Assert.Empty(uploadedData);

            /* test de-duping */
            fakeTime = fakeTime.AddMinutes(20);
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 5.0)));
            await worker.Scrape(ct);
            fakeTime = fakeTime.AddMinutes(10);
            await worker.Scrape(ct);
            await worker.Upload(ct);
            fakeTime = fakeTime.AddMinutes(1);
            Assert.Equal(10, uploadedData.Count());
            fakeTime = fakeTime.AddMinutes(1);
            await worker.Upload(ct);
            Assert.Empty(uploadedData);

            /* test mix of de-duping and not */
            fakeTime = fakeTime.AddMinutes(20);
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 7.0)));
            await worker.Scrape(ct);
            fakeTime = fakeTime.AddMinutes(10);
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", i % 2 == 0 ? 7.0 : 8.0)));
            await worker.Scrape(ct);
            fakeTime = fakeTime.AddMinutes(10);
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 7.0)));
            await worker.Scrape(ct);
            fakeTime = fakeTime.AddMinutes(1);
            await worker.Upload(ct);
            Assert.Equal(20, uploadedData.Count());
            await worker.Upload(ct);
            fakeTime = fakeTime.AddMinutes(1);
            Assert.Empty(uploadedData);
        }

        [Fact]
        public async Task TestNoOverlap()
        {
            /* Setup mocks */
            TaskCompletionSource<bool> scrapeTaskSource = new TaskCompletionSource<bool>();
            TaskCompletionSource<bool> uploadTaskSource = new TaskCompletionSource<bool>();

            var scraper = new Mock<IMetricsScraper>();
            scraper.Setup(s => s.ScrapeEndpointsAsync(CancellationToken.None)).Returns(async () =>
            {
                await scrapeTaskSource.Task;
                return this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 1.0)).ToArray());
            });

            var storage = new Mock<IMetricsStorage>();

            var uploader = new Mock<IMetricsPublisher>();
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), It.IsAny<CancellationToken>())).Returns(async () => await uploadTaskSource.Task);

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            /* test scraper first */
            var scrapeTask = worker.Scrape(CancellationToken.None);
            await Task.Delay(1);
            var uploadTask = worker.Upload(CancellationToken.None);
            await Task.Delay(1);

            uploadTaskSource.SetResult(true);
            await Task.Delay(1);

            Assert.False(scrapeTask.IsCompleted);
            Assert.False(uploadTask.IsCompleted);
            scrapeTaskSource.SetResult(true);
            await Task.Delay(1);

            await Task.WhenAll(scrapeTask, uploadTask);

            /* test uploader first */
            scrapeTaskSource = new TaskCompletionSource<bool>();
            uploadTaskSource = new TaskCompletionSource<bool>();

            uploadTask = worker.Upload(CancellationToken.None);
            await Task.Delay(1);
            scrapeTask = worker.Scrape(CancellationToken.None);
            await Task.Delay(1);

            scrapeTaskSource.SetResult(true);
            await Task.Delay(1);

            Assert.False(scrapeTask.IsCompleted);
            Assert.False(uploadTask.IsCompleted);
            uploadTaskSource.SetResult(true);
            await Task.Delay(1);

            await Task.WhenAll(scrapeTask, uploadTask);
        }

        private IEnumerable<Metric> PrometheousMetrics(IEnumerable<(string name, double value)> modules)
        {
            string dataPoints = string.Join("\n", modules.Select(module => $@"
edgeagent_module_start_total{{iothub=""lefitche-hub-3.azure-devices.net"",edge_device=""device4"",instance_number=""1"",module_name=""{module.name}"",module_version=""1.0""}} {module.value}
"));
            string metricsString = $@"
# HELP edgeagent_module_start_total Start command sent to module
# TYPE edgeagent_module_start_total counter
{dataPoints}
";

            return PrometheusMetricsParser.ParseMetrics(DateTime.UtcNow, metricsString);
        }

        public void Dispose()
        {
            this.tempDirectory.Dispose();
        }
    }
}
