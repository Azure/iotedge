// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using App.Metrics;
    using App.Metrics.Formatters;

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

        public void Dispose()
        {
            this.cts.Cancel();
            this.processTask.Wait();
            this.httpListener.Stop();
            ((IDisposable)this.httpListener)?.Dispose();
        }

        async Task ProcessRequests()
        {
            try
            {
                while (!this.cts.IsCancellationRequested)
                {
                    HttpListenerContext context = await this.httpListener.GetContextAsync();
                    using (Stream output = context.Response.OutputStream)
                    {
                        MetricsDataValueSource metricsData = this.metricsRoot.Snapshot.Get();
                        IMetricsOutputFormatter formatter = this.metricsRoot.DefaultOutputMetricsFormatter;
                        await formatter.WriteAsync(output, metricsData, this.cts.Token);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
