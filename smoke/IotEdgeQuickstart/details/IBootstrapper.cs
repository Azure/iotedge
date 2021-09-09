// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public interface IBootstrapper
    {
        Task UpdatePackageState();

        Task VerifyDependenciesAreInstalled();

        Task VerifyModuleIsRunning(string name);

        Task Install();

        /* This will set up the device with edge agent image 1.0 if the Bootstrapper is not passed an agent image to use.
           This is usually desired because, if aziot-edged instead starts a the desired agent image, edgeAgent will not update its initial environment variables, createOptions, etc.
           A deployment is necessary to start the desired agent image. */
        Task Configure(DeviceProvisioningMethod method, Option<string> agentImage, string hostname, Option<string> parentHostname, string deviceCaCert, string deviceCaPk, string deviceCaCerts, LogLevel runtimeLogLevel);

        Task Start();

        Task Stop();

        Task Reset();
    }
}
