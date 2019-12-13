// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Storage;
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
            Metric[] testData = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 1.0)).ToArray()).ToArray();
            Metric ReplaceWithNewValue(Metric[] list, int index, double newValue)
            {
                return list[index] = new Metric(list[index].TimeGeneratedUtc, list[index].Name, newValue, list[index].Tags);
            }

            /* Setup mocks */
            var scraper = new Mock<IMetricsScraper>();
            scraper.Setup(s => s.ScrapeEndpointsAsync(CancellationToken.None)).ReturnsAsync(testData);

            var storage = new Mock<IMetricsStorage>();
            IEnumerable<Metric> storedValues = Enumerable.Empty<Metric>();
            storage.Setup(s => s.StoreMetricsAsync(It.IsAny<IEnumerable<Metric>>())).Callback((Action<IEnumerable<Metric>>)(data => storedValues = data)).Returns(Task.CompletedTask);

            var uploader = new Mock<IMetricsPublisher>();

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            // all values are stored
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(1, scraper.Invocations.Count);
            Assert.Equal(1, storage.Invocations.Count);
            Assert.Equal(testData, storedValues);

            // duplicates don't get stored
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(2, scraper.Invocations.Count);
            Assert.Empty(storedValues);

            Metric newMetric = ReplaceWithNewValue(testData, 1, 2.5);
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(3, scraper.Invocations.Count);
            Assert.Equal(newMetric, storedValues.Single());

            // multiple values get stored
            Metric[] newMetrics = new Metric[]
            {
                ReplaceWithNewValue(testData, 1, 5.5),
                ReplaceWithNewValue(testData, 5, 6),
                ReplaceWithNewValue(testData, 7, 7.5),
            };
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(4, scraper.Invocations.Count);
            Assert.Equal(newMetrics, storedValues);

            // multiple duplicates don't get stored
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(5, scraper.Invocations.Count);
            Assert.Empty(storedValues);
        }

        [Fact]
        public async Task TestBasicUploading()
        {
            /* Setup mocks */
            var scraper = new Mock<IMetricsScraper>();

            var storage = new Mock<IMetricsStorage>();
            storage.Setup(s => s.GetAllMetricsAsync()).ReturnsAsync(Enumerable.Empty<Metric>());

            var uploader = new Mock<IMetricsPublisher>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), It.IsAny<CancellationToken>())).Callback((Action<IEnumerable<Metric>, CancellationToken>)((data, __) => uploadedData = data)).ReturnsAsync(true);

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            /* test */
            await worker.Upload(CancellationToken.None);
            uploadedData.ToList();
            Assert.Equal(1, storage.Invocations.Count);
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
            storage.Setup(s => s.GetAllMetricsAsync()).ReturnsAsync(metrics);
            var uploader = new Mock<IMetricsPublisher>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), It.IsAny<CancellationToken>())).Callback((Action<IEnumerable<Metric>, CancellationToken>)((d, _) => uploadedData = d)).ReturnsAsync(true);

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            /* test */
            await worker.Upload(CancellationToken.None);
            Assert.Equal(metrics.OrderBy(x => x.Tags), uploadedData.OrderBy(x => x.Tags));
            Assert.Equal(1, storage.Invocations.Count);
            Assert.Equal(1, uploader.Invocations.Count);
        }

        [Fact]
        public async Task TestUploadIsLazy()
        {
            /* test data */
            int metricsCalls = 0;
            IEnumerable<Metric> Metrics()
            {
                metricsCalls++;
                return Enumerable.Range(1, 10).Select(i => new Metric(DateTime.UtcNow, "1", 3, $"{i}"));
            }

            /* Setup mocks */
            var scraper = new Mock<IMetricsScraper>();

            var storage = new Mock<IMetricsStorage>();
            storage.Setup(s => s.GetAllMetricsAsync()).ReturnsAsync(Metrics);

            var uploader = new Mock<IMetricsPublisher>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), It.IsAny<CancellationToken>())).Callback((Action<IEnumerable<Metric>, CancellationToken>)((d, _) => uploadedData = d)).ReturnsAsync(true);

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            /* test */
            await worker.Upload(CancellationToken.None);
            int numMetrics = 0;
            foreach (Metric metric in uploadedData)
            {
                Assert.Equal(numMetrics++ / 10 + 1, metricsCalls);
            }

            Assert.Equal(1, storage.Invocations.Count);
            Assert.Equal(1, uploader.Invocations.Count);
        }

        [Fact]
        public async Task TestScrapeAndUpload()
        {
            CancellationToken ct = CancellationToken.None;

            var scraper = new Mock<IMetricsScraper>();
            var scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 1.0)));
            scraper.Setup(s => s.ScrapeEndpointsAsync(ct)).ReturnsAsync(() => scrapeResults);

            var storage = new MetricsFileStorage(this.tempDirectory.CreateTempDir());

            var uploader = new Mock<IMetricsPublisher>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), ct)).Callback((Action<IEnumerable<Metric>, CancellationToken>)((d, _) => uploadedData = d.ToArray())).ReturnsAsync(true);

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage, uploader.Object);

            /* test without de-duping */
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 1.0)));
            await worker.Scrape(ct);
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 2.0)));
            await worker.Scrape(ct);
            await worker.Upload(ct);
            Assert.Equal(20, uploadedData.Count());
            await worker.Upload(ct);
            Assert.Empty(uploadedData);

            /* test de-duping */
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 5.0)));
            await worker.Scrape(ct);
            await worker.Scrape(ct);
            await worker.Upload(ct);
            Assert.Equal(10, uploadedData.Count());
            await worker.Upload(ct);
            Assert.Empty(uploadedData);

            /* test mix of de-duping and not */
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 7.0)));
            await worker.Scrape(ct);
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", i % 2 == 0 ? 7.0 : 8.0)));
            await worker.Scrape(ct);
            scrapeResults = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", 7.0)));
            await worker.Scrape(ct);
            await worker.Upload(ct);
            Assert.Equal(20, uploadedData.Count());
            await worker.Upload(ct);
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
