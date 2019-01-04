// Copyright (c) Microsoft. All rights reserved.
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
            bool useWebSockets)
            : base(iothubConnectionString, eventhubCompatibleEndpointWithEntityPath, deviceId, certificateFileName, edgeHostName, useWebSockets)
        {
        }

        public async Task RunAsync()
        {
            // This test assumes that there is an edge deployment running as transparent gateway.
            try
            {
                await this.InitializeServerCerts();
                await this.GetOrCreateDeviceIdentity();
                await this.ConnectToEdgeAndSendData();
                await this.VerifyDataOnIoTHub();
                await this.VerifyDirectMethod();
            }
            catch (Exception)
            {
                Console.WriteLine("** Oops, there was a problem.");
                this.KeepDeviceIdentity();
                throw;
            }
            finally
            {
                // only remove the identity if we created it; if it already existed in IoT Hub then leave it alone
                await this.MaybeDeleteDeviceIdentity();
            }
        }
    }
}
