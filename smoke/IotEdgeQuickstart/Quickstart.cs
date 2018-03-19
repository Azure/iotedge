// Copyright (c) Microsoft. All rights reserved.

// ReSharper disable ArrangeThisQualifier
namespace IotEdgeQuickstart
{
    using System;
    using System.Threading.Tasks;

    public class Quickstart : Details
    {
        readonly bool leaveRunning;

        public Quickstart(
            string iotedgectlArchivePath,
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            string registryAddress,
            string registryUser,
            string registryPassword,
            string imageTag,
            string deviceId,
            string hostname,
            bool leaveRunning) :
            base(iotedgectlArchivePath, iothubConnectionString, eventhubCompatibleEndpointWithEntityPath,
                registryAddress, registryUser, registryPassword, imageTag, deviceId, hostname)
        {
            this.leaveRunning = leaveRunning;
        }

        public async Task RunAsync()
        {
            // This test assumes that no existing at-scale deployment will bring in this edge device and overwrite
            // its config. This could happen, for example, if someone were to create an at-scale deployment on the
            // test hub with the target condition: "NOT deviceId=''". Since this is an unlikely scenario, we won't
            // invest the effort to guard against it.

            await VerifyEdgeIsNotAlreadyInstalled(); // don't accidentally overwrite an edge installation on a dev machine
            await VerifyDockerIsInstalled();
            await VerifyPipIsInstalled();
            Task.WaitAll(
                InstallIotedgectl(),
                GetOrCreateEdgeDeviceIdentity());

            try
            {
                try
                {
                    await IotedgectlSetup();
                    await IotedgectlStart();
                    await VerifyEdgeAgentIsRunning();
                    await VerifyEdgeAgentIsConnectedToIotHub();
                    await DeployTempSensorToEdgeDevice();
                    await VerifyTempSensorIsRunning();
                    await VerifyTempSensorIsSendingDataToIotHub();
                }
                catch(Exception)
                {
                    Console.WriteLine("** Oops, there was a problem. We'll stop the IoT Edge runtime, but we'll leave it configured so you can investigate.");
                    await IotedgectlStop();
                    throw;
                }

                if (!this.leaveRunning)
                {
                    await IotedgectlStop();
                    await IotedgectlUninstall();
                }
            }
            finally
            {
                if (!this.leaveRunning)
                {
                    // only remove the identity if we created it; if it already existed in IoT Hub then leave it alone
                    await MaybeDeleteEdgeDeviceIdentity();
                }
            }
        }
    }
}
