// Copyright (c) Microsoft. All rights reserved.
namespace TransactableModuleSample 
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
            Console.WriteLine("TransactableModuleSample  Main() started.");

            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();

            IConfiguration configuration = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("config/appsettings.json", optional: true)
               .AddEnvironmentVariables()
               .Build();

            var purchaseInfoProvider = await PurchaseInfoProvider.CreateAsync(configuration);

            while (!cts.IsCancellationRequested)
            {
                Console.WriteLine("Getting purchase");
                PurchaseInfo purchase = await purchaseInfoProvider.GetPurchaseAsync(cts.Token);
                Console.WriteLine($"publisherId: {purchase.PublisherId}, offerId: {purchase.OfferId}, planId: {purchase.PlanId}");
                await Task.Delay(TimeSpan.FromSeconds(60), cts.Token);
            }

            await WhenCanceled(cts.Token);

            Console.WriteLine("TransactableModuleSample  Main() finished.");
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
