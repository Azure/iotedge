// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator.Storage
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using ModuleUtil.NetworkControllerResult;
    using Newtonsoft.Json;

    /// <summary>
    /// This class takes in the client side TestOperationResult and converts it to the test side TestOperationResult
    /// by adding NetworkStatus to it. If the TestOperationResult is of type Network, then it can change the network status too.
    /// </summary>
    public class StoragePreparer
    {
        static readonly ILogger Logger = Microsoft.Azure.Devices.Edge.ModuleUtil.ModuleUtil.CreateLogger(nameof(StoragePreparer));

        static AtomicBoolean currentlyOnline = new AtomicBoolean(true);
        public static TestResultCoordinator.TestOperationResult PrepareTestOperationResult(TestOperationResult testOperationResult)
        {
            Preconditions.CheckNotNull(testOperationResult, nameof(testOperationResult));
            if (Microsoft.Azure.Devices.Edge.ModuleUtil.TestOperationResultType.Network.ToString().Equals(testOperationResult.Type))
            {
                NetworkControllerResult networkControllerResult = (NetworkControllerResult)JsonConvert.DeserializeObject(testOperationResult.Result, typeof(NetworkControllerResult));
                switch (networkControllerResult.NetworkStatus)
                {
                    case NetworkStatus.Offline:
                        Logger.LogInformation($"Setting CurrentlyOnline to {!networkControllerResult.Enabled}");
                        currentlyOnline.Set(!networkControllerResult.Enabled);
                        break;
                    default:
                        Logger.LogInformation($"Setting CurrentlyOnline to true");
                        currentlyOnline.Set(true);
                        break;
                }
            }

            testOperationResult.NetworkOn = currentlyOnline.Get();
            return testOperationResult;
        }
    }
}
