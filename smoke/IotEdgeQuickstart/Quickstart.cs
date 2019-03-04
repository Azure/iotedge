// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart
{
    using System;
    using System.Threading.Tasks;
    using IotEdgeQuickstart.Details;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Quickstart : Details.Details
    {
        readonly LeaveRunning leaveRunning;
        readonly bool noDeployment;
        readonly bool noVerify;
        readonly string verifyDataFromModule;

        public Quickstart(
            IBootstrapper bootstrapper,
            Option<RegistryCredentials> credentials,
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            UpstreamProtocolType upstreamProtocol,
            string imageTag,
            string deviceId,
            string hostname,
            LeaveRunning leaveRunning,
            bool noDeployment,
            bool noVerify,
            string verifyDataFromModule,
            Option<string> deploymentFileName,
            Option<string> twinTestFileName,
            string deviceCaCert,
            string deviceCaPk,
            string deviceCaCerts,
            bool optimizedForPerformance,
            LogLevel runtimeLogLevel,
            bool cleanUpExistingDeviceOnSuccess)
            : base(bootstrapper, credentials, iothubConnectionString, eventhubCompatibleEndpointWithEntityPath, upstreamProtocol, imageTag, deviceId, hostname, deploymentFileName, twinTestFileName, deviceCaCert, deviceCaPk, deviceCaCerts, optimizedForPerformance, runtimeLogLevel, cleanUpExistingDeviceOnSuccess)
        {
            this.leaveRunning = leaveRunning;
            this.noDeployment = noDeployment;
            this.noVerify = noVerify;
            this.verifyDataFromModule = verifyDataFromModule;
        }

        public async Task RunAsync()
        {
            // This test assumes that no existing at-scale deployment will bring in this edge device and overwrite
            // its config. This could happen, for example, if someone were to create an at-scale deployment on the
            // test hub with the target condition: "NOT deviceId=''". Since this is an unlikely scenario, we won't
            // invest the effort to guard against it.
            await this.VerifyEdgeIsNotAlreadyActive(); // don't accidentally overwrite an edge configuration on a dev machine
            await this.VerifyBootstrapperDependencies();
            await this.InstallBootstrapper();

            try
            {
                await this.GetOrCreateEdgeDeviceIdentity();
                await this.ConfigureBootstrapper();

                try
                {
                    await this.StartBootstrapper();
                    await this.VerifyEdgeAgentIsRunning();
                    await this.VerifyEdgeAgentIsConnectedToIotHub();
                    if (!this.noDeployment)
                    {
                        await this.DeployToEdgeDevice();
                        if (!this.noVerify)
                        {
                            await this.VerifyDataOnIoTHub(this.verifyDataFromModule);
                            await this.VerifyTwinAsync();
                        }

                        if (this.leaveRunning == LeaveRunning.Core)
                        {
                            await this.RemoveTempSensorFromEdgeDevice();
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("** Oops, there was a problem. We'll stop the IoT Edge runtime, but we'll leave it configured so you can investigate.");
                    Console.WriteLine($"Exception: {e}");
                    this.KeepEdgeDeviceIdentity();
                    await this.StopBootstrapper();
                    throw;
                }

                if (this.leaveRunning == LeaveRunning.None)
                {
                    await this.StopBootstrapper();
                    await this.ResetBootstrapper();
                }
            }
            finally
            {
                if (this.leaveRunning == LeaveRunning.None)
                {
                    // only remove the identity if we created it; if it already existed in IoT Hub then leave it alone
                    await this.MaybeDeleteEdgeDeviceIdentity();
                }
            }
        }
    }
}
