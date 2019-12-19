// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Akka.Streams.Implementation.Fusing;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Publisher;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Storage;
    using Microsoft.Azure.Devices.Edge.Agent.Diagnostics.Util;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Moq;
    using Xunit;
    using Xunit.Sdk;

    public class MetricsWorkerTest : IDisposable
    {
        TempDirectory tempDirectory = new TempDirectory();
        Random rand = new Random();

        [Fact]
        public async Task TestScraping()
        {
            /* Setup mocks */
            Metric[] testData = new Metric[0];
            var scraper = new Mock<IMetricsScraper>();
            scraper.Setup(s => s.ScrapeEndpointsAsync(CancellationToken.None)).ReturnsAsync(() => testData);

            var storage = new Mock<IMetricsStorage>();
            IEnumerable<Metric> storedValues = Enumerable.Empty<Metric>();
            storage.Setup(s => s.StoreMetricsAsync(It.IsAny<IEnumerable<Metric>>())).Callback((Action<IEnumerable<Metric>>)(data => storedValues = data)).Returns(Task.CompletedTask);

            var uploader = new Mock<IMetricsPublisher>();

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            // all values are stored
            testData = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", this.rand.NextDouble())).ToArray()).ToArray();
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(1, scraper.Invocations.Count);
            Assert.Equal(1, storage.Invocations.Count);
            Assert.Equal(testData, storedValues);

            testData = this.PrometheousMetrics(Enumerable.Range(1, 10).Select(i => ($"module_{i}", this.rand.NextDouble())).ToArray()).ToArray();
            await worker.Scrape(CancellationToken.None);
            Assert.Equal(2, scraper.Invocations.Count);
            Assert.Equal(2, storage.Invocations.Count);
            Assert.Equal(testData, storedValues);
        }

        [Fact]
        public async Task TestBasicUploading()
        {
            /* Setup mocks */
            var scraper = new Mock<IMetricsScraper>();
            var storage = new Mock<IMetricsStorage>();
            storage.Setup(s => s.GetAllMetricsAsync()).ReturnsAsync(Enumerable.Empty<Metric>());

            TaskCompletionSource<object> uploadStarted = new TaskCompletionSource<object>();
            TaskCompletionSource<bool> finishUpload = new TaskCompletionSource<bool>();
            var uploader = new Mock<IMetricsPublisher>();
            IEnumerable<Metric> uploadedData = Enumerable.Empty<Metric>();
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), It.IsAny<CancellationToken>())).Callback((Action<IEnumerable<Metric>, CancellationToken>)((data, __) =>
            {
                uploadedData = data;
                uploadStarted.SetResult(null);
            })).Returns(finishUpload.Task);

            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            /* test */
            Task workerTask = worker.Upload(CancellationToken.None);
            await uploadStarted.Task;
            uploadedData.ToList();
            Assert.Equal(1, uploader.Invocations.Count);
            Assert.Single(storage.Invocations.Where(i => i.Method.Name == "GetAllMetricsAsync"));
            Assert.Empty(storage.Invocations.Where(i => i.Method.Name == "RemoveAllReturnedMetricsAsync"));
            finishUpload.SetResult(true);
            Assert.Single(storage.Invocations.Where(i => i.Method.Name == "GetAllMetricsAsync"));
            Assert.Single(storage.Invocations.Where(i => i.Method.Name == "RemoveAllReturnedMetricsAsync"));
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
            Assert.Single(storage.Invocations.Where(i => i.Method.Name == "GetAllMetricsAsync"));
            Assert.Single(storage.Invocations.Where(i => i.Method.Name == "RemoveAllReturnedMetricsAsync"));
            Assert.Single(uploader.Invocations);
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

            Assert.Single(storage.Invocations.Where(i => i.Method.Name == "GetAllMetricsAsync"));
            Assert.Single(storage.Invocations.Where(i => i.Method.Name == "RemoveAllReturnedMetricsAsync"));
            Assert.Single(uploader.Invocations);
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

        [Fact]
        public async Task TestRetry()
        {
            int targetRetries = 10;
            /* Setup mocks */
            var scraper = new Mock<IMetricsScraper>();
            var storage = new Mock<IMetricsStorage>();
            storage.Setup(s => s.GetAllMetricsAsync()).ReturnsAsync(Enumerable.Empty<Metric>());

            var uploader = new Mock<IMetricsPublisher>();
            int actualRetries = 0;
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => ++actualRetries == targetRetries);

            MetricsWorker.RetryStrategy = new FakeRetryStrategy();
            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            /* test */
            await worker.Upload(CancellationToken.None);
            Assert.Equal(targetRetries, actualRetries);
            Assert.Equal(targetRetries, uploader.Invocations.Count);
            Assert.Single(storage.Invocations.Where(i => i.Method.Name == "RemoveAllReturnedMetricsAsync"));
        }

        [Fact]
        public async Task TestRetryFail()
        {
            int maxRetries = 5;
            /* Setup mocks */
            var scraper = new Mock<IMetricsScraper>();
            var storage = new Mock<IMetricsStorage>();
            storage.Setup(s => s.GetAllMetricsAsync()).ReturnsAsync(Enumerable.Empty<Metric>());

            var uploader = new Mock<IMetricsPublisher>();
            uploader.Setup(u => u.PublishAsync(It.IsAny<IEnumerable<Metric>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            MetricsWorker.RetryStrategy = new FakeRetryStrategy(maxRetries);
            MetricsWorker worker = new MetricsWorker(scraper.Object, storage.Object, uploader.Object);

            /* test */
            await worker.Upload(CancellationToken.None);
            Assert.Equal(maxRetries + 1, uploader.Invocations.Count); // It tries once, then retries maxRetryTimes.
            Assert.Single(storage.Invocations.Where(i => i.Method.Name == "RemoveAllReturnedMetricsAsync"));
        }

        class FakeRetryStrategy : RetryStrategy
        {
            int maxRetries;

            public FakeRetryStrategy(int maxRetries = int.MaxValue)
                : base(true)
            {
                this.maxRetries = maxRetries;
            }

            public override ShouldRetry GetShouldRetry()
            {
                return this.ShouldRetry;
            }

            bool ShouldRetry(int i, Exception ex, out TimeSpan delay)
            {
                delay = TimeSpan.Zero;
                return i < this.maxRetries;
            }
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
