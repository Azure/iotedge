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
        readonly MetricsListenerConfig listenerConfig;

        Task processTask;
        ILogger logger;

        public MetricsListener(MetricsListenerConfig listenerConfig, IMetricsProvider metricsProvider)
        {
            this.listenerConfig = Preconditions.CheckNotNull(listenerConfig, nameof(listenerConfig));
            string url = GetMetricsListenerUrlPrefix(listenerConfig);
            this.httpListener = new HttpListener();
            this.httpListener.Prefixes.Add(url);
            this.metricsProvider = Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
        }

        public void Start(ILogger logger)
        {
            this.logger = logger;
            this.logger?.LogInformation($"Starting metrics listener on {this.listenerConfig}");
            this.httpListener.Start();
            this.processTask = this.ProcessRequests();
        }

        public void Dispose()
        {
            this.logger?.LogInformation("Stopping metrics listener");
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
                this.logger?.LogWarning($"Error processing metrics request - {e}");
            }
        }

        static string GetMetricsListenerUrlPrefix(MetricsListenerConfig listenerConfig)
            => string.Format(CultureInfo.InvariantCulture, MetricsUrlPrefixFormat, listenerConfig.Host, listenerConfig.Port, listenerConfig.Suffix.Trim('/', ' '));
    }
}
