// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public class MetricsListener : IDisposable
    {
        readonly HttpListener httpListener;
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly IMetricsProvider metricsProvider;
        readonly Task processTask;

        public MetricsListener(string url, IMetricsProvider metricsProvider)
        {
            this.httpListener = new HttpListener();
            this.httpListener.Prefixes.Add(url);
            this.httpListener.Start();
            this.metricsProvider = metricsProvider;
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
                        byte[] snapshot = await this.metricsProvider.GetSnapshot(this.cts.Token);
                        await output.WriteAsync(snapshot, 0, snapshot.Length, this.cts.Token);
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
