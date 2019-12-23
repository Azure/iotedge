// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Storage
{
    using System;
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkControllerResult;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// This class takes in the client side TestOperationResult and converts it to the test side TestOperationResult
    /// by adding NetworkStatus to it. If the TestOperationResult is of type Network, then it can change the network status too.
    /// </summary>
    public class StoragePreparer
    {
        static readonly ILogger Logger = Microsoft.Azure.Devices.Edge.ModuleUtil.ModuleUtil.CreateLogger(nameof(StoragePreparer));

        static AtomicBoolean currentlyOnline = new AtomicBoolean(true);
        static long dateTimeTicks = 0;
        public static TestOperationResult PrepareTestOperationResult(TestOperationResult testOperationResult)
        {
            Preconditions.CheckNotNull(testOperationResult, nameof(testOperationResult));
            if (Microsoft.Azure.Devices.Edge.ModuleUtil.TestOperationResultType.Network.ToString().Equals(testOperationResult.Type))
            {
                NetworkControllerResult networkControllerResult = (NetworkControllerResult)JsonConvert.DeserializeObject(testOperationResult.Result, typeof(NetworkControllerResult));
                switch (networkControllerResult.NetworkControllerType)
                {
                    case NetworkControllerType.Offline:
                        if (networkControllerResult.NetworkControllerStatus == NetworkControllerStatus.Enabled)
                        {
                            Logger.LogInformation($"Setting Online Status to false");
                            currentlyOnline.Set(false);
                        }
                        else
                        {
                            Logger.LogInformation($"Setting Online Status to true");
                            currentlyOnline.Set(true);
                        }

                        dateTimeTicks = Interlocked.Exchange(ref dateTimeTicks, DateTime.Now.Ticks);
                        break;
                    default:
                        throw new NotImplementedException($"Storage preparation for {networkControllerResult.NetworkControllerType} is not yet implemented");
                }
            }

            testOperationResult.NetworkOnline = currentlyOnline.Get();
            testOperationResult.NetworkLastUpdatedTime = new DateTime(dateTimeTicks);
            return testOperationResult;
        }
    }
}
