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
        readonly bool noVerify;
        readonly bool bypassEdgeInstallation;
        readonly string verifyDataFromModule;
        readonly bool dpsProvisionTest;

        public Quickstart(
            IBootstrapper bootstrapper,
            Option<RegistryCredentials> credentials,
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            UpstreamProtocolType upstreamProtocol,
            Option<string> proxy,
            string imageTag,
            string deviceId,
            string hostname,
            Option<string> parentHostname,
            Option<string> parentEdgeDevice,
            LeaveRunning leaveRunning,
            bool noVerify,
            bool bypassEdgeInstallation,
            string verifyDataFromModule,
            Option<string> deploymentFileName,
            Option<string> twinTestFileName,
            string deviceCaCert,
            string deviceCaPk,
            string deviceCaCerts,
            bool optimizedForPerformance,
            bool initializeWithAgentArtifact,
            LogLevel runtimeLogLevel,
            bool cleanUpExistingDeviceOnSuccess,
            Option<DPSAttestation> dpsAttestation)
            : base(bootstrapper, credentials, iothubConnectionString, eventhubCompatibleEndpointWithEntityPath, upstreamProtocol, proxy, imageTag, deviceId, hostname, parentHostname, parentEdgeDevice, deploymentFileName, twinTestFileName, deviceCaCert, deviceCaPk, deviceCaCerts, optimizedForPerformance, initializeWithAgentArtifact, runtimeLogLevel, cleanUpExistingDeviceOnSuccess, dpsAttestation)
        {
            this.leaveRunning = leaveRunning;
            this.noVerify = noVerify;
            this.bypassEdgeInstallation = bypassEdgeInstallation;
            this.verifyDataFromModule = verifyDataFromModule;
            this.dpsProvisionTest = dpsAttestation.HasValue;
        }

        public async Task RunAsync()
        {
            // This test assumes that no existing at-scale deployment will bring in this edge device and overwrite
            // its config. This could happen, for example, if someone were to create an at-scale deployment on the
            // test hub with the target condition: "NOT deviceId=''". Since this is an unlikely scenario, we won't
            // invest the effort to guard against it.
            if (!this.bypassEdgeInstallation)
            {
                await this.UpdatePackageState();
                await this.VerifyBootstrapperDependencies();
                await this.InstallBootstrapper();
            }

            try
            {
                await this.GetOrCreateEdgeDeviceIdentity();
                await this.ConfigureBootstrapper();

                try
                {
                    await this.StartBootstrapper();
                    await this.VerifyEdgeAgentIsRunning();
                    await this.VerifyEdgeAgentIsConnectedToIotHub();

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
