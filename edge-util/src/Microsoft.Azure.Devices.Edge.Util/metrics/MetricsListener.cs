// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class MetricsListener : IMetricsListener
    {
        const string MetricsUrlPrefixFormat = "http://{0}:{1}/{2}/";

        readonly HttpListener httpListener;
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly IMetricsProvider metricsProvider;
        readonly ILogger logger;
        readonly string url;

        Task processTask;

        public MetricsListener(string host, int port, string suffix, IMetricsProvider metricsProvider, ILogger logger)
        {
            this.httpListener = new HttpListener();
            this.url = GetMetricsListenerUrlPrefix(host, port, suffix);
            this.httpListener.Prefixes.Add(this.url);
            this.metricsProvider = Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
            this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
        }

        public void Start()
        {
            this.logger.LogInformation($"Starting metrics listener on {this.url}");
            this.httpListener.Start();
            this.processTask = this.ProcessRequests();
        }

        public void Dispose()
        {
            this.logger.LogInformation("Stopping metrics listener");
            this.cts.Cancel();
            this.processTask.Wait();
            this.httpListener.Stop();
            ((IDisposable)this.httpListener).Dispose();
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

        static string GetMetricsListenerUrlPrefix(string host, int port, string urlSuffix)
            => string.Format(CultureInfo.InvariantCulture, MetricsUrlPrefixFormat, host, port.ToString(), urlSuffix.Trim('/', ' '));
    }
}
