// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Startup = TestServer.Startup;

    public class EdgeletFixture : IDisposable
    {
        const int DefaultPort = 50002;
        public string ServiceUrl = ServiceHost.Instance.Url;

        class ServiceHost
        {
            readonly Task webHostTask;
            readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            ServiceHost()
            {
                this.webHostTask = BuildWebHost(new string[0], DefaultPort).RunAsync(cancellationTokenSource.Token);
                this.Url = $"http://localhost:{DefaultPort}";
            }

            public static ServiceHost Instance { get; } = new ServiceHost();

            public string Url { get; }

            static IWebHost BuildWebHost(string[] args, int port) =>
                WebHost.CreateDefaultBuilder(args)
                .UseUrls($"http://*:{port}")
                .UseStartup<Startup>()
                .Build();
        }

        #region IDisposable Support
        // Don't dispose the Server, in case another test thread is using it. 
        public void Dispose()
        { }
        #endregion
    }
}
