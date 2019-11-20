// Copyright (c) Microsoft. All rights reserved.
namespace DemoApp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using DevOpsLib;
    using DevOpsLib.VstsModels;

    class Program
    {
        static async Task Main(string[] args)
        {
            AzureActiveDirectory ADD = new AzureActiveDirectory(
                "72f988bf-86f1-41af-91ab-2d7cd011db47",
                "e754afe9-3fbb-46eb-981f-a52f99df1b9e",
                "oN:JF-/jnylQBBIfOBG7G1ynS/fa9NK1");

            var accessToken = await ADD.GetAccessToken(AzureLogAnalytics.AzureResource);
            Console.WriteLine("Access Token: " + accessToken);
            Console.WriteLine(" ");

            AzureLogAnalytics test1 = new AzureLogAnalytics();
            var queryResult = await test1.GetKqlQuery(
                ADD,
                "fdf47b96-87f3-4b86-90b9-d83e2deae8a0",
                "lhPipeline_CL");


            Console.WriteLine("Query Results: " + queryResult);
            Console.WriteLine(" ");

            accessToken = await ADD.GetAccessToken("https://api.loganalytics.io");
            Console.WriteLine("Access Token1: " + accessToken);
            Console.WriteLine(" ");

            Console.WriteLine(" ");
            Console.WriteLine("Access Token2a: " + accessToken);
            Console.WriteLine(" ");
            accessToken = await ADD.GetAccessToken("https://management.azure.com/");
            Console.WriteLine("Access Token2b: " + accessToken);
            Console.WriteLine(" ");

            accessToken = await ADD.GetAccessToken("https://api.loganalytics.io");
            Console.WriteLine("Access Token3: " + accessToken);
            Console.WriteLine(" ");

            accessToken = await ADD.GetAccessToken("https://api.loganalytics.io");
            Console.WriteLine("Access Token4: " + accessToken);
            Console.WriteLine(" ");

            accessToken = await ADD.GetAccessToken("https://api.loganalytics.io");
            Console.WriteLine("Access Token5: " + accessToken);
            Console.WriteLine(" ");
            Console.ReadKey();
        }
    }
}
