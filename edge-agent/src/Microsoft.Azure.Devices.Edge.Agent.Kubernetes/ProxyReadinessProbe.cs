// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class ProxyReadinessProbe
    {
        readonly string apiVersion;

        readonly HttpClient client;

        public ProxyReadinessProbe(Uri url, string apiVersion)
        {
            this.apiVersion = apiVersion;
            this.client = new HttpClient { BaseAddress = url };
        }

        async Task<ProxyReadiness> CheckAsync(CancellationToken token)
        {
            try
            {
                using (HttpResponseMessage response = await this.client.GetAsync($"/systeminfo?api-version={this.apiVersion}", token))
                {
                    return response.StatusCode == HttpStatusCode.OK ? ProxyReadiness.Ready : ProxyReadiness.Failed;
                }
            }
            catch
            {
                return ProxyReadiness.NotReady;
            }
        }

        public async Task WaitUntilProxyIsReady(CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    throw new ProxyReadinessProbeException("All proxy readiness attempts exhausted.");
                }

                CancellationTokenSource tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                ProxyReadiness readiness = await this.CheckAsync(tokenSource.Token);
                Events.CheckHealth(readiness);

                if (readiness == ProxyReadiness.Ready)
                {
                    break;
                }
            }
        }

        enum ProxyReadiness
        {
            Ready,
            NotReady,
            Failed
        }

        static class Events
        {
            const int IdStart = KubernetesEventIds.KubernetesProxyHealthProbe;
            static readonly ILogger Log = Logger.Factory.CreateLogger<ProxyReadinessProbe>();

            enum EventIds
            {
                CheckHealth = IdStart,
            }

            internal static void CheckHealth(ProxyReadiness readiness)
                => Log.LogDebug((int)EventIds.CheckHealth, $"Proxy container readiness state: {readiness}");
        }
    }

    public class ProxyReadinessProbeException : Exception
    {
        public ProxyReadinessProbeException(string message)
            : base(message)
        {
        }
    }
}
