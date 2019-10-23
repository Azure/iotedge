using Microsoft.Azure.Devices.Edge.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Devices.Edge.Agent.MetricsCollector
{
    public class Worker
    {
        private readonly IScraper scraper;
        private readonly IFileStorage storage;
        private readonly IMetricsUpload uploader;
        private readonly ISystemTime systemTime;
        private readonly MetricsParser metricsParser = new MetricsParser();
        private readonly AsyncLock scrapeUploadLock = new AsyncLock();

        private DateTime lastUploadTime = DateTime.MinValue;
        private Dictionary<int, Metric> metrics = new Dictionary<int, Metric>();

        public Worker(IScraper scraper, IFileStorage storage, IMetricsUpload uploader, ISystemTime systemTime = null)
        {
            this.scraper = scraper;
            this.storage = storage;
            this.uploader = uploader;
            this.systemTime = systemTime ?? SystemTime.Instance;
        }

        public async Task Start(TimeSpan scrapingInterval, TimeSpan uploadInterval, CancellationToken cancellationToken)
        {
            Task scraper = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(scrapingInterval, cancellationToken);
                    await Scrape(cancellationToken);
                }
            }, cancellationToken);

            Task uploader = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(uploadInterval, cancellationToken);
                    await Upload(cancellationToken);
                }
            }, cancellationToken);

            await Task.WhenAll(scraper, uploader);
        }

        private async Task Scrape(CancellationToken cancellationToken)
        {
            using (await scrapeUploadLock.GetLock(cancellationToken))
            {
                Console.WriteLine($"\n\n\nScraping Metrics");
                List<Metric> metricsToPersist = new List<Metric>();

                foreach (var moduleResult in await scraper.ScrapeAsync(cancellationToken))
                {
                    var scrapedMetrics = metricsParser.ParseMetrics(systemTime.UtcNow, moduleResult.Value);

                    foreach (Metric scrapedMetric in scrapedMetrics)
                    {
                        if (metrics.TryGetValue(scrapedMetric.GetValuelessHash(), out Metric oldMetric))
                        {
                            if (oldMetric.Value.Equals(scrapedMetric.Value))
                            {
                                continue;
                            }
                            metricsToPersist.Add(oldMetric);
                        }
                        metrics[scrapedMetric.GetValuelessHash()] = scrapedMetric;
                    }

                }

                if (metricsToPersist.Count != 0)
                {
                    Console.WriteLine($"Storing metrics");
                    storage.AddScrapeResult(Newtonsoft.Json.JsonConvert.SerializeObject(metricsToPersist));
                }
            }
        }

        private async Task Upload(CancellationToken cancellationToken)
        {
            using (await scrapeUploadLock.GetLock(cancellationToken))
            {
                Console.WriteLine($"\n\n\nUploading Metrics");
                await uploader.UploadAsync(GetMetricsToUpload(lastUploadTime), cancellationToken);
                lastUploadTime = systemTime.UtcNow;
            }
        }

        private IEnumerable<Metric> GetMetricsToUpload(DateTime lastUploadTime)
        {
            foreach (KeyValuePair<DateTime, Func<string>> data in storage.GetData(lastUploadTime))
            {
                var temp = data.Value();
                var fileMetrics = Newtonsoft.Json.JsonConvert.DeserializeObject<Metric[]>(temp) ?? Enumerable.Empty<Metric>();
                foreach (Metric metric in fileMetrics)
                {
                    yield return metric;
                }
            }

            foreach (Metric metric in metrics.Values)
            {
                yield return metric;
            }

            storage.RemoveOldEntries(lastUploadTime);
            metrics.Clear();
        }
    }

    public class AsyncLock
    {
        public class OwnedLock : IDisposable
        {
            private SemaphoreSlim semaphore;
            internal OwnedLock(SemaphoreSlim semaphore)
            {
                this.semaphore = semaphore;
            }

            public void Dispose()
            {
                semaphore.Release();
            }
        }

        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public async Task<OwnedLock> GetLock(CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);
            return new OwnedLock(semaphore);
        }
    }
}
