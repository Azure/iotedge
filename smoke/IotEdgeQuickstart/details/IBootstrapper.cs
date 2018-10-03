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

        Task Configure(string connectionString, string image, string hostname, string deviceCaCert, string deviceCaPk, string deviceCaCerts, LogLevel runtimeLogLevel);

        Task Start();

        Task Stop();

        Task Reset();
    }
}
