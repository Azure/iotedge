// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;

    public class MetricsProvider : IMetricsProvider
    {
        readonly IMetricsRoot metricsRoot;
        readonly MetricsListener metricsListener;

        MetricsProvider(IMetricsRoot metricsRoot, MetricsListener metricsListener)
        {
            this.metricsRoot = metricsRoot;
            this.metricsListener = metricsListener;
        }

        public static MetricsProvider CreatePrometheusExporter(string url)
        {
            IMetricsRoot metricsRoot = new MetricsBuilder()
                .OutputMetrics.AsPrometheusPlainText()
                .Build();
            var metricsListener = new MetricsListener(url, metricsRoot);
            var metricsProvider = new MetricsProvider(metricsRoot, metricsListener);
            return metricsProvider;
        }

        public ICounter CreateCounter(string name, Dictionary<string, string> tags) =>
            new MetricsCounter(name, this.metricsRoot.Measure.Counter, tags);
    }


    public class MetricsListener : IDisposable
    {
        readonly HttpListener httpListener;
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly IMetricsRoot metricsRoot;
        readonly Task processTask;

        public MetricsListener(string url, IMetricsRoot metricsRoot)
        {
            this.httpListener = new HttpListener();
            this.httpListener.Prefixes.Add(url);
            this.httpListener.Start();
            this.metricsRoot = metricsRoot;
            this.processTask = this.ProcessRequests();
        }

        async Task ProcessRequests()
        {
            try
            {
                while (!this.cts.IsCancellationRequested)
                {
                    HttpListenerContext context = await this.httpListener.GetContextAsync();
                    using (var output = context.Response.OutputStream)
                    {
                        var metricsData = this.metricsRoot.Snapshot.Get();
                        var formatter = this.metricsRoot.DefaultOutputMetricsFormatter;
                        await formatter.WriteAsync(output, metricsData, this.cts.Token);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void Dispose()
        {
            this.cts.Cancel();
            this.processTask.Wait();
            this.httpListener.Stop();
            ((IDisposable)this.httpListener)?.Dispose();
        }
    }
}
