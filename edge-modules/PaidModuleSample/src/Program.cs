// Copyright (c) Microsoft. All rights reserved.
namespace PaidModuleSample
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;

    class Program
    {
        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Console.WriteLine("PaidModuleSample Main() started.");

            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();

            IConfiguration configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("config/appsettings.json", optional: true)
               .AddEnvironmentVariables()
               .Build();

            string iotHubHostName = configuration.GetValue<string>("IOTEDGE_IOTHUBHOSTNAME");
            string deviceId = configuration.GetValue<string>("IOTEDGE_DEVICEID");
            string moduleId = configuration.GetValue<string>("IOTEDGE_MODULEID");
            string generationId = configuration.GetValue<string>("IOTEDGE_MODULEGENERATIONID");
            string workloadUri = configuration.GetValue<string>("IOTEDGE_WORKLOADURI");
            string gateway = configuration.GetValue<string>("IOTEDGE_GATEWAYHOSTNAME");

            var purchaseInfoProvider = new PurchaseInfoProvider(iotHubHostName, gateway, deviceId, moduleId, generationId, workloadUri);

            await purchaseInfoProvider.StartGetPurchaseAsync(deviceId, moduleId, cts.Token);

            await WhenCanceled(cts.Token);

            Console.WriteLine("PaidModuleSample Main() finished.");
            return 0;
        }

        public static Task WhenCanceled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }
    }
}
