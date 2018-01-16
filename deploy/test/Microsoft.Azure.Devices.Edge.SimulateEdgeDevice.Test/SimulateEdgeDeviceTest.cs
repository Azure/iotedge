// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.SimulateEdgeDevice.Test
{
    using System.Threading.Tasks;
    using Xunit;

    using static TestHelpers;

    public class SimulateEdgeDeviceTest
    {
        [Fact]
        [Deploy]
        public async Task Run()
        {
            // This test assumes that no existing at-scale deployment will bring in this edge device and overwrite
            // its config. This could happen, for example, if someone were to create an at-scale deployment on the
            // test hub with the target condition: "NOT deviceId=''". Since this is an unlikely scenario, we won't
            // invest the effort to guard against it.

            await VerifyEdgeIsNotAlreadyInstalled(); // don't accidentally overwrite an edge installation on a dev machine
            await VerifyDockerIsInstalled();
            await VerifyPipIsInstalled();
            Task iotedgectlInstalled = InstallIotedgectl();
            Task<DeviceContext> context = RegisterNewEdgeDeviceAsync();

            try
            {
                await iotedgectlInstalled;
                await IotedgectlSetup(await context);
                await IotedgectlStart();

                try
                {
                    await VerifyEdgeAgentIsRunning();
                    await VerifyEdgeAgentIsConnectedToIotHub(await context);
                    await DeployTempSensorToEdgeDevice(await context);
                    await VerifyTempSensorIsRunning();
                    await VerifyTempSensorIsSendingDataToIotHub(await context);
                }
                finally
                {
                    await IotedgectlStop();
                    await IotedgectlUninstall();
                }
            }
            finally
            {
                await UnregisterEdgeDevice(await context);
            }
        }
    }
}
