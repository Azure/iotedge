// Copyright (c) Microsoft. All rights reserved.
namespace DirectMethodCloudSender
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    class Program
    {
        const string RouteOutputName = "output1";
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DirectMethodCloudSender");

        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Logger.LogInformation("DirectMethodCloudSender Main() started.");

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config/appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string serviceClientConnectionString = Preconditions.CheckNonWhiteSpace(configuration.GetValue<string>("ServiceClientConnectionString"), nameof(serviceClientConnectionString));
            // Get device id of this device, exposed as a system variable by the iot edge runtime
            string targetDeviceId = configuration.GetValue<string>("IOTEDGE_DEVICEID");
            string targetModuleId = configuration.GetValue("TargetModuleId", "DirectMethodReceiver");
            TimeSpan dmDelay = configuration.GetValue("DirectMethodDelay", TimeSpan.FromSeconds(5));
            Uri analyzerUrl = configuration.GetValue("AnalyzerUrl", new Uri("http://analyzer:15000"));

            AnalyzerClient analyzerClient = new AnalyzerClient { BaseUrl = analyzerUrl.AbsoluteUri };

            (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

            await CallDirectMethodFromCloud(serviceClientConnectionString, targetDeviceId, targetModuleId, dmDelay, analyzerClient, cts);

            completed.Set();
            handler.ForEach(h => GC.KeepAlive(h));
            Logger.LogInformation("DirectMethodCloudSender Main() finished.");
            return 0;
        }

        static async Task CallDirectMethodFromCloud(
            string serviceClientConnectionString,
            string deviceId,
            string moduleId,
            TimeSpan delay,
            AnalyzerClient analyzerClient,
            CancellationTokenSource cts)
        {
            Logger.LogInformation("CallDirectMethodFromCloud started.");
            ServiceClient serviceClient = null;

            try
            {
                int count = 1;

                IotHubConnectionStringBuilder iotHubConnectionStringBuilder = IotHubConnectionStringBuilder.Create(serviceClientConnectionString);
                Logger.LogInformation($"Prepare to call Direct Method from cloud ({iotHubConnectionStringBuilder.IotHubName}) on device [{deviceId}] module [{moduleId}]");

                serviceClient = ServiceClient.CreateFromConnectionString(serviceClientConnectionString, Microsoft.Azure.Devices.TransportType.Amqp);
                var cloudToDeviceMethod = new CloudToDeviceMethod("HelloWorldMethod").SetPayloadJson("{ \"Message\": \"Hello\" }");

                while (!cts.Token.IsCancellationRequested)
                {
                    Logger.LogInformation($"Calling Direct Method from cloud ({iotHubConnectionStringBuilder.IotHubName}) on device [{deviceId}] module [{moduleId}] of count {count}.");

                    try
                    {
                        CloudToDeviceMethodResult result = await serviceClient.InvokeDeviceMethodAsync(deviceId, moduleId, cloudToDeviceMethod, CancellationToken.None);

                        if (result.Status == (int)HttpStatusCode.OK)
                        {
                            Logger.LogDebug($"Calling Direct Method from cloud with count {count} returned with status code {result.Status}.");
                        }
                        else
                        {
                            Logger.LogError($"Calling Direct Method from cloud with count {count} failed with status code {result.Status}.");
                        }

                        await CallAnalyzerToReportStatusAsync(moduleId, result, analyzerClient);

                        count++;
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Exception caught with count {count}: {e}");
                    }

                    await Task.Delay(delay, cts.Token);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception caught: {e}");
                throw;
            }
            finally
            {
                Logger.LogInformation("Close connection for service client and module client");
                if (serviceClient != null)
                {
                    await serviceClient.CloseAsync();
                }
            }

            Logger.LogInformation("CallDirectMethodFromCloud finished.");
        }

        static async Task CallAnalyzerToReportStatusAsync(string moduleId, CloudToDeviceMethodResult result, AnalyzerClient analyzerClient)
        {
            try
            {
                await analyzerClient.ReportResultAsync(new TestOperationResult { Source = moduleId, Result = result.Status.ToString(), CreatedAt = DateTime.UtcNow, Type = "LegacyDirectMethod" });
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed call to report status to analyzer: {e}");
            }
        }
    }
}
