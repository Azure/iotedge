// Copyright (c) Microsoft. All rights reserved.
// ReSharper disable ArrangeThisQualifier
namespace LeafDevice
{
    using System;
    using System.Threading.Tasks;

    public class LeafDevice : Details.Details
    {
        public LeafDevice(
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            string deviceId,
            string certificateFileName,
            string edgeHostName,
            bool useWebSockets) :
            base(iothubConnectionString, eventhubCompatibleEndpointWithEntityPath, deviceId, certificateFileName, edgeHostName, useWebSockets)
        {
        }

        public async Task RunAsync()
        {
            // This test assumes that there is an edge deployment running as transparent gateway.
            try
            {
                await this.InitializeServerCerts();
                await GetOrCreateDeviceIdentity();
                await ConnectToEdgeAndSendData();
                await this.VerifyDataOnIoTHub();
                await this.VerifyDirectMethod();
            }
            catch (Exception)
            {
                Console.WriteLine("** Oops, there was a problem.");
                KeepDeviceIdentity();
                throw;
            }
            finally
            {
                // only remove the identity if we created it; if it already existed in IoT Hub then leave it alone
                await MaybeDeleteDeviceIdentity();
            }
        }
    }
}
