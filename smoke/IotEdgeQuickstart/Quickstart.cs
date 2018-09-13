// Copyright (c) Microsoft. All rights reserved.

// ReSharper disable ArrangeThisQualifier
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
            string imageTag,
            string deviceId,
            string hostname,
            LeaveRunning leaveRunning,
            bool noDeployment,
            bool noVerify,
            string verifyDataFromModule,
            Option<string> deploymentFileName,
            string deviceCaCert,
            string deviceCaPk,
            string deviceCaCerts,
            bool optmizedForPerformance) :
            base(bootstrapper, credentials, iothubConnectionString, eventhubCompatibleEndpointWithEntityPath, imageTag, deviceId, hostname, deploymentFileName, deviceCaCert, deviceCaPk, deviceCaCerts, optmizedForPerformance)
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

            await VerifyEdgeIsNotAlreadyActive(); // don't accidentally overwrite an edge configuration on a dev machine
            await VerifyBootstrapperDependencies();
            await InstallBootstrapper();

            try
            {
                await GetOrCreateEdgeDeviceIdentity();
                await ConfigureBootstrapper();

                try
                {
                    await StartBootstrapper();
                    await VerifyEdgeAgentIsRunning();
                    await VerifyEdgeAgentIsConnectedToIotHub();
                    if (!this.noDeployment)
                    {
                        await DeployToEdgeDevice();
                        if (!this.noVerify)
                        {
                            await VerifyTempSensorIsRunning();
                            await this.VerifyDataOnIoTHub(this.verifyDataFromModule);
                        }

                        if (this.leaveRunning == LeaveRunning.Core)
                        {
                            await RemoveTempSensorFromEdgeDevice();
                        }
                    }
                }
                catch(Exception)
                {
                    Console.WriteLine("** Oops, there was a problem. We'll stop the IoT Edge runtime, but we'll leave it configured so you can investigate.");
                    KeepEdgeDeviceIdentity();
                    await StopBootstrapper();
                    throw;
                }

                if (this.leaveRunning == LeaveRunning.None)
                {
                    await StopBootstrapper();
                    await ResetBootstrapper();
                }
            }
            finally
            {
                if (this.leaveRunning == LeaveRunning.None)
                {
                    // only remove the identity if we created it; if it already existed in IoT Hub then leave it alone
                    await MaybeDeleteEdgeDeviceIdentity();
                }
            }
        }
    }
}
