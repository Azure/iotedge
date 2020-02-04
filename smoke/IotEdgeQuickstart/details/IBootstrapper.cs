// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System.Threading.Tasks;

    public interface IBootstrapper
    {
        Task VerifyNotActive();

        Task VerifyDependenciesAreInstalled();

        Task VerifyModuleIsRunning(string name);

        Task Install();

        /* This will set up the device with edge agent image 1.0.
           This is necessary because, if iotedged instead starts a the desired agent image, edgeAgent will not update its initial environment variables, createOptions, etc.
           A deployment is necessary to start the desired agent image. */
        Task Configure(DeviceProvisioningMethod method, string hostname, string deviceCaCert, string deviceCaPk, string deviceCaCerts, LogLevel runtimeLogLevel);

        Task Start();

        Task Stop();

        Task Reset();
    }
}
