// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common.WorkloadTestServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;

    public class WorkloadFixture : IDisposable
    {
        public readonly string ServiceUrl = ServiceHost.Instance.Url;
        const int DefaultPort = 50003;

        ServiceHost serviceHost;

        #region IDisposable Support

        // Don't dispose the Server, in case another test thread is using it.
        public void Dispose()
        {
        }

        #endregion

        public WorkloadFixture()
        {
            this.serviceHost = new ServiceHost();
        }

        class ServiceHost
        {
            readonly Task webHostTask;
            readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            public ServiceHost()
            {
                this.webHostTask = CreateHostBuilder(new string[0], DefaultPort).Build().RunAsync(this.cancellationTokenSource.Token);
                this.Url = $"http://localhost:{DefaultPort}";
            }

            public static ServiceHost Instance { get; } = new ServiceHost();

            public string Url { get; }

            static IHostBuilder CreateHostBuilder(string[] args, int port) =>
                Host.CreateDefaultBuilder(args)
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder
                            .UseUrls($"http://*:{port}")
                            .UseStartup<Startup>();
                    });
        }
    }
}
