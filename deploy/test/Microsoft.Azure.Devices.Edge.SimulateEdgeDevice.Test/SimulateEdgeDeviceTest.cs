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
            // TODO: fail test if any previous at-scale deployment tries to overwrite this edge device's config

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
